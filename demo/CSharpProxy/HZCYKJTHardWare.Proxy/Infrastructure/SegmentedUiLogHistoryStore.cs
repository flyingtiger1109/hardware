using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    /// <summary>
    /// UI-visible session log source. The complete daily log is kept intact,
    /// while this store retains only the latest in-memory tail and indexes older
    /// UI records through a temporary fixed-size offset file.
    /// </summary>
    internal sealed class SegmentedUiLogHistoryStore : IDisposable
    {
        private const int HistoryPageSize = 500;
        private const int MaxCachedHistoryPages = 8;
        private const int TailScanIntervalMs = 1000;
        private const int IndexRecordSize = 12; // Int64 offset + Int32 byte length

        private readonly object _sync = new object();
        private readonly int _maxRecentEntries;
        private readonly List<string> _recentLines = new List<string>();
        private readonly Dictionary<long, string> _tailLineCache = new Dictionary<long, string>();
        private readonly Queue<long> _tailLineOrder = new Queue<long>();
        private readonly Dictionary<long, string[]> _pageCache = new Dictionary<long, string[]>();
        private readonly LinkedList<long> _cacheLru = new LinkedList<long>();
        private readonly HashSet<long> _loadingPages = new HashSet<long>();
        private readonly Timer _tailScanTimer;

        private string _date;
        private string _filePath;
        private string _indexFilePath;
        private long _tailOffset;
        private long _indexedLineCount;
        private int _generation;
        private int _tailScanActive;
        private bool _disposed;

        public SegmentedUiLogHistoryStore(int maxRecentEntries)
        {
            _maxRecentEntries = Math.Max(1000, maxRecentEntries);
            _tailScanTimer = new Timer(TailScanTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public event EventHandler Changed;

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    var count = _indexedLineCount + _recentLines.Count;
                    return count >= int.MaxValue ? int.MaxValue : (int)count;
                }
            }
        }

        public void Start(string date)
        {
            SwitchDay(date);
        }

        public void AddPersistedLine(string date, string line)
        {
            if (string.IsNullOrEmpty(line) || _disposed)
                return;

            if (!string.Equals(date, CurrentDate, StringComparison.Ordinal))
                SwitchDay(date);

            lock (_sync)
            {
                _recentLines.Add(line);
                if (_recentLines.Count > _maxRecentEntries)
                    _recentLines.RemoveAt(0);
            }

            RaiseChanged();
        }

        public bool TryGetLine(int index, out string line)
        {
            line = null;
            lock (_sync)
            {
                var logicalIndex = (long)index;
                var total = _indexedLineCount + _recentLines.Count;
                if (index < 0 || logicalIndex >= total)
                    return false;

                if (logicalIndex >= _indexedLineCount)
                {
                    line = _recentLines[(int)(logicalIndex - _indexedLineCount)];
                    return true;
                }

                if (_tailLineCache.TryGetValue(logicalIndex, out line))
                    return true;

                var pageIndex = logicalIndex / HistoryPageSize;
                string[] cachedPage;
                if (_pageCache.TryGetValue(pageIndex, out cachedPage))
                {
                    var pageOffset = (int)(logicalIndex % HistoryPageSize);
                    if (pageOffset < cachedPage.Length)
                    {
                        line = cachedPage[pageOffset];
                        TouchCachedPageLocked(pageIndex);
                        return true;
                    }

                    RemoveCachedPageLocked(pageIndex);
                }

                QueuePageLoadLocked(pageIndex);
            }

            return false;
        }

        public void PrefetchRange(int startIndex, int endIndex)
        {
            lock (_sync)
            {
                if (_indexedLineCount == 0)
                    return;

                var firstIndex = Math.Max(0, startIndex);
                var lastIndex = Math.Min((long)Math.Max(startIndex, endIndex), _indexedLineCount - 1);
                if (firstIndex > lastIndex)
                    return;

                var firstPage = (long)firstIndex / HistoryPageSize;
                var lastPage = lastIndex / HistoryPageSize;
                for (var pageIndex = firstPage; pageIndex <= lastPage; pageIndex++)
                    QueuePageLoadLocked(pageIndex);
            }
        }

        private string CurrentDate
        {
            get
            {
                lock (_sync)
                {
                    return _date;
                }
            }
        }

        private void SwitchDay(string date)
        {
            if (string.IsNullOrEmpty(date))
                date = DateTime.Now.ToString("yyyyMMdd");

            string oldIndexFilePath;
            string indexFilePath;
            lock (_sync)
            {
                if (string.Equals(_date, date, StringComparison.Ordinal) && !_disposed)
                    return;

                oldIndexFilePath = _indexFilePath;
                _date = date;
                _filePath = Logger.GetLogFilePath(date);
                _indexFilePath = CreateIndexFilePath(date);
                _recentLines.Clear();
                _tailLineCache.Clear();
                _tailLineOrder.Clear();
                _pageCache.Clear();
                _cacheLru.Clear();
                _loadingPages.Clear();
                _indexedLineCount = 0;
                _generation++;
                _tailOffset = GetFileLength(_filePath);
                indexFilePath = _indexFilePath;
            }

            DeleteIndexFileQuietly(oldIndexFilePath);
            CreateEmptyIndexFile(indexFilePath);
            _tailScanTimer.Change(TailScanIntervalMs, TailScanIntervalMs);
            RaiseChanged();
        }

        private void TailScanTimerCallback(object state)
        {
            if (_disposed || Interlocked.CompareExchange(ref _tailScanActive, 1, 0) != 0)
                return;

            try
            {
                ScanTail();
            }
            finally
            {
                Interlocked.Exchange(ref _tailScanActive, 0);
            }
        }

        private void ScanTail()
        {
            string filePath;
            long startOffset;
            int generation;
            lock (_sync)
            {
                if (_disposed || string.IsNullOrEmpty(_filePath))
                    return;

                filePath = _filePath;
                startOffset = _tailOffset;
                generation = _generation;
            }

            var currentLength = GetFileLength(filePath);
            if (currentLength <= startOffset)
                return;

            List<ScannedLogLine> scannedLines;
            long nextOffset;
            try
            {
                scannedLines = ScanCompleteLines(filePath, startOffset, currentLength, out nextOffset);
            }
            catch
            {
                return;
            }

            if (scannedLines.Count == 0)
                return;

            var changed = false;
            lock (_sync)
            {
                if (_disposed || generation != _generation || startOffset != _tailOffset)
                    return;

                var matchedLines = new List<ScannedLogLine>();
                var recentOffset = 0;
                for (var i = 0; i < scannedLines.Count && recentOffset < _recentLines.Count; i++)
                {
                    var scanned = scannedLines[i];
                    if (!string.Equals(_recentLines[recentOffset], scanned.Text, StringComparison.Ordinal))
                        continue;

                    matchedLines.Add(scanned);
                    recentOffset++;
                }

                if (matchedLines.Count > 0 && AppendIndexReferencesLocked(matchedLines))
                {
                    for (var i = 0; i < matchedLines.Count; i++)
                    {
                        AddTailLineLocked(_indexedLineCount, matchedLines[i].Text);
                        _indexedLineCount++;
                    }

                    _recentLines.RemoveRange(0, matchedLines.Count);
                    changed = true;
                }

                // Non-UI diagnostics and records before this EXE started are skipped.
                _tailOffset = nextOffset;
            }

            if (changed)
                RaiseChanged();
        }

        private bool AppendIndexReferencesLocked(List<ScannedLogLine> lines)
        {
            if (string.IsNullOrEmpty(_indexFilePath))
                return false;

            try
            {
                using (var stream = new FileStream(_indexFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                using (var writer = new BinaryWriter(stream))
                {
                    stream.Seek(0, SeekOrigin.End);
                    for (var i = 0; i < lines.Count; i++)
                    {
                        writer.Write(lines[i].Reference.Offset);
                        writer.Write(lines[i].Reference.Length);
                    }
                    writer.Flush();
                    stream.Flush();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AddTailLineLocked(long index, string line)
        {
            _tailLineCache[index] = line;
            _tailLineOrder.Enqueue(index);
            while (_tailLineOrder.Count > _maxRecentEntries)
            {
                var oldestIndex = _tailLineOrder.Dequeue();
                _tailLineCache.Remove(oldestIndex);
            }

            RemoveCachedPageLocked(index / HistoryPageSize);
        }

        private void QueuePageLoadLocked(long pageIndex)
        {
            if (_pageCache.ContainsKey(pageIndex) || _loadingPages.Contains(pageIndex) || _disposed)
                return;

            var startIndex = pageIndex * HistoryPageSize;
            if (startIndex >= _indexedLineCount)
                return;

            var count = (int)Math.Min(HistoryPageSize, _indexedLineCount - startIndex);
            var filePath = _filePath;
            var indexFilePath = _indexFilePath;
            var generation = _generation;
            _loadingPages.Add(pageIndex);

            Task.Run(() => LoadHistoryPage(pageIndex, startIndex, count, filePath, indexFilePath, generation));
        }

        private void LoadHistoryPage(long pageIndex, long startIndex, int count, string filePath,
            string indexFilePath, int generation)
        {
            var lines = new string[count];
            try
            {
                var references = ReadIndexReferences(indexFilePath, startIndex, count);
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    for (var i = 0; i < lines.Length; i++)
                    {
                        lines[i] = i < references.Length
                            ? ReadLine(stream, references[i])
                            : "[日志历史读取失败]";
                    }
                }
            }
            catch
            {
                for (var i = 0; i < lines.Length; i++)
                    lines[i] = "[日志历史读取失败]";
            }

            lock (_sync)
            {
                _loadingPages.Remove(pageIndex);
                if (_disposed || generation != _generation ||
                    !string.Equals(filePath, _filePath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(indexFilePath, _indexFilePath, StringComparison.OrdinalIgnoreCase))
                    return;

                _pageCache[pageIndex] = lines;
                TouchCachedPageLocked(pageIndex);
                while (_pageCache.Count > MaxCachedHistoryPages)
                {
                    var oldest = _cacheLru.First;
                    if (oldest == null)
                        break;
                    _pageCache.Remove(oldest.Value);
                    _cacheLru.RemoveFirst();
                }
            }

            RaiseChanged();
        }

        private static LogLineReference[] ReadIndexReferences(string indexFilePath, long startIndex, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(indexFilePath))
                return new LogLineReference[0];

            using (var stream = new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream))
            {
                stream.Position = startIndex * IndexRecordSize;
                var references = new List<LogLineReference>(count);
                for (var i = 0; i < count && stream.Position + IndexRecordSize <= stream.Length; i++)
                    references.Add(new LogLineReference(reader.ReadInt64(), reader.ReadInt32()));
                return references.ToArray();
            }
        }

        private static List<ScannedLogLine> ScanCompleteLines(string filePath, long startOffset, long endOffset,
            out long nextOffset)
        {
            nextOffset = startOffset;
            var result = new List<ScannedLogLine>();
            if (endOffset <= startOffset || !File.Exists(filePath))
                return result;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Position = startOffset;
                var buffer = new byte[64 * 1024];
                var lineStart = startOffset;
                var position = startOffset;
                while (position < endOffset)
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, endOffset - position);
                    var bytesRead = stream.Read(buffer, 0, bytesToRead);
                    if (bytesRead <= 0)
                        break;

                    for (var i = 0; i < bytesRead; i++)
                    {
                        position++;
                        if (buffer[i] != (byte)'\n')
                            continue;

                        result.Add(new ScannedLogLine(
                            new LogLineReference(lineStart, checked((int)(position - lineStart))), null));
                        lineStart = position;
                    }
                }

                nextOffset = lineStart;
            }

            if (result.Count == 0)
                return result;

            using (var reader = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                for (var i = 0; i < result.Count; i++)
                {
                    var scanned = result[i];
                    scanned.Text = ReadLine(reader, scanned.Reference);
                    result[i] = scanned;
                }
            }

            return result;
        }

        private static string ReadLine(FileStream stream, LogLineReference reference)
        {
            var buffer = new byte[reference.Length];
            stream.Position = reference.Offset;
            var bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                var read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
                if (read <= 0)
                    break;
                bytesRead += read;
            }

            return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimStart('\uFEFF').TrimEnd('\r', '\n');
        }

        private static long GetFileLength(string filePath)
        {
            try
            {
                return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string CreateIndexFilePath(string date)
        {
            var logFilePath = Logger.GetLogFilePath(date);
            var logDir = Path.GetDirectoryName(logFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(logDir, ".HZCYKJTHardWare.UiLogIndex." + Guid.NewGuid().ToString("N") + ".bin");
        }

        private static void CreateEmptyIndexFile(string indexFilePath)
        {
            try
            {
                using (new FileStream(indexFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                }
            }
            catch
            {
                // The live tail remains usable if history paging is unavailable.
            }
        }

        private static void DeleteIndexFileQuietly(string indexFilePath)
        {
            if (string.IsNullOrEmpty(indexFilePath))
                return;

            try
            {
                if (File.Exists(indexFilePath))
                    File.Delete(indexFilePath);
            }
            catch
            {
                // Cleanup must not affect service shutdown or daily logging.
            }
        }

        private void TouchCachedPageLocked(long pageIndex)
        {
            var node = _cacheLru.Find(pageIndex);
            if (node != null)
                _cacheLru.Remove(node);
            _cacheLru.AddLast(pageIndex);
        }

        private void RemoveCachedPageLocked(long pageIndex)
        {
            if (!_pageCache.Remove(pageIndex))
                return;

            var node = _cacheLru.Find(pageIndex);
            if (node != null)
                _cacheLru.Remove(node);
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler == null || _disposed)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch
            {
                // The store remains independent from UI observers.
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _tailScanTimer.Dispose();

            string indexFilePath;
            lock (_sync)
            {
                indexFilePath = _indexFilePath;
                _recentLines.Clear();
                _tailLineCache.Clear();
                _tailLineOrder.Clear();
                _pageCache.Clear();
                _cacheLru.Clear();
                _loadingPages.Clear();
                _indexFilePath = null;
            }

            DeleteIndexFileQuietly(indexFilePath);
        }

        private struct LogLineReference
        {
            public LogLineReference(long offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public long Offset;
            public int Length;
        }

        private struct ScannedLogLine
        {
            public ScannedLogLine(LogLineReference reference, string text)
            {
                Reference = reference;
                Text = text;
            }

            public LogLineReference Reference;
            public string Text;
        }
    }
}
