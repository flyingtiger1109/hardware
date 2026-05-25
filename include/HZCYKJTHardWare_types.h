#ifndef HZCYKJTHARDWARE_TYPES_H
#define HZCYKJTHARDWARE_TYPES_H

#ifdef __cplusplus
extern "C" {
#endif

/* Internal error codes (logged to file, not exposed to caller). Public API returns 1/0. */
#define HZCYKJTHardWare_RET_OK                         1
#define HZCYKJTHardWare_RET_FAILED                    -1
#define HZCYKJTHardWare_RET_NOT_INITIALIZED           -2
#define HZCYKJTHardWare_RET_INVALID_PARAM             -3
#define HZCYKJTHardWare_RET_BUFFER_TOO_SMALL          -4
#define HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE      -5
#define HZCYKJTHardWare_RET_HTTP_FAILED               -6
#define HZCYKJTHardWare_RET_TIMEOUT                   -7
#define HZCYKJTHardWare_RET_INVALID_HWND              -8
#define HZCYKJTHardWare_RET_PREVIEW_ALREADY_RUNNING   -9
#define HZCYKJTHardWare_RET_PREVIEW_NOT_RUNNING      -10
#define HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED   -11
#define HZCYKJTHardWare_RET_PARSE_JSON_FAILED        -12
#define HZCYKJTHardWare_RET_BASE64_FAILED            -13
#define HZCYKJTHardWare_RET_SAVE_FILE_FAILED         -14
#define HZCYKJTHardWare_RET_DEVICE_BUSY              -15
#define HZCYKJTHardWare_RET_REQUEST_NOT_FOUND        -16
#define HZCYKJTHardWare_RET_REQUEST_EXPIRED          -17
#define HZCYKJTHardWare_RET_UNSUPPORTED              -18
#define HZCYKJTHardWare_RET_PREVIEW_RENDER_FAILED    -19
#define HZCYKJTHardWare_RET_RTSP_URL_EMPTY           -20
#define HZCYKJTHardWare_RET_VLC_INIT_FAILED          -21
#define HZCYKJTHardWare_RET_ALREADY_INITIALIZED      -22
#define HZCYKJTHardWare_RET_TERMINAL_NOT_SELECTED    -23
#define HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID   -24
#define HZCYKJTHardWare_RET_SUBNET_DETECT_FAILED     -25
#define HZCYKJTHardWare_RET_TERMINAL_SWITCH_FAILED   -26
#define HZCYKJTHardWare_RET_MULTI_NIC_NEED_CONFIG    -27
#define HZCYKJTHardWare_RET_CONFIG_NOT_FOUND         -28
#define HZCYKJTHardWare_RET_CONFIG_INVALID           -29
#define HZCYKJTHardWare_RET_CONFIG_FIELD_MISSING     -30

/* Event types */
#define HZCYKJTHardWare_EVENT_TERMINAL_ONLINE              1001
#define HZCYKJTHardWare_EVENT_TERMINAL_OFFLINE             1002
#define HZCYKJTHardWare_EVENT_TERMINAL_SWITCHED            1003

#define HZCYKJTHardWare_EVENT_PROCESS_STARTED              1101
#define HZCYKJTHardWare_EVENT_PROCESS_ENDED                1102

#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED       1201
#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STOPPED       1202
#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED        1203

#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED  1301
#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STOPPED  1302
#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED   1303

/* Reserved legacy events. Face/fingerprint capture currently return synchronously. */
#define HZCYKJTHardWare_EVENT_FACE_CAPTURE_SUCCESS         1401
#define HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED          1402
#define HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_SUCCESS  1501
#define HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED   1502

#define HZCYKJTHardWare_EVENT_OCR_SUCCESS                  1601
#define HZCYKJTHardWare_EVENT_OCR_FAILED                   1602

#define HZCYKJTHardWare_EVENT_REQUEST_TIMEOUT              1701

#define HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STARTED         1801
#define HZCYKJTHardWare_EVENT_IRIS_PREVIEW_STOPPED         1802
#define HZCYKJTHardWare_EVENT_IRIS_PREVIEW_FAILED          1803
#define HZCYKJTHardWare_EVENT_IRIS_CAPTURE_SUCCESS         1804
#define HZCYKJTHardWare_EVENT_IRIS_CAPTURE_FAILED          1805
#define HZCYKJTHardWare_EVENT_NFC_CARD_SUCCESS             1806
#define HZCYKJTHardWare_EVENT_NFC_CARD_FAILED              1807

#define HZCYKJTHardWare_EVENT_PLATE_PREVIEW_STARTED        1901
#define HZCYKJTHardWare_EVENT_PLATE_PREVIEW_STOPPED        1902
#define HZCYKJTHardWare_EVENT_PLATE_PREVIEW_FAILED         1903

#define HZCYKJTHardWare_EVENT_ERROR                        1999

/* Resource types */
#define HZCYKJTHardWare_RESOURCE_FACE_IMAGE        "face_image"
#define HZCYKJTHardWare_RESOURCE_FINGERPRINT_IMAGE "fingerprint_image"
#define HZCYKJTHardWare_RESOURCE_OCR_DOCUMENT      "ocr_document"
#define HZCYKJTHardWare_RESOURCE_IRIS_IMAGE        "iris_image"
#define HZCYKJTHardWare_RESOURCE_NFC_CARD          "nfc_card"
#define HZCYKJTHardWare_RESOURCE_PLATE_IMAGE      "plate_image"

/*
 * Unified event data.
 * All string pointers are valid only during the callback. Copy them inside the
 * callback if the caller needs to keep them.
 */
#pragma pack(push, 1)
typedef struct HZCYKJTHardWare_EVENT
{
    int struct_size;
    int event_type;

    const char* request_id;
    const char* resource_type;

    int status;
    const char* error_code;
    const char* message;

    const char* terminal_base_url;
    int terminal_index;

    const char* save_path;
    const char* raw_json;

    const void* data;
    int data_size;

    const char* ic_number;
    const char* mrz;
} HZCYKJTHardWare_EVENT;
#pragma pack(pop)

typedef void (__stdcall *THZCYKJTHardWareEventCallback)(const char* eventJson);

#ifdef __cplusplus
}
#endif

#endif /* HZCYKJTHARDWARE_TYPES_H */
