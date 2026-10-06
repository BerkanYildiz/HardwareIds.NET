#ifndef HARDWAREIDS_H
#define HARDWAREIDS_H

/*
 * HardwareIds C API: takes a snapshot of the hardware identifiers of the local Windows computer, as JSON.
 *
 * The JSON is the one HardwareIds.NET writes (JsonSerializer.Serialize(Hwid)), so snapshots taken from C, C++ or .NET
 * can be compared directly. Link against HardwareIds.dll (define nothing), or against the static library (define
 * HARDWAREIDS_STATIC, which the CMake target does for you).
 */

#include <stddef.h>

#if defined(HARDWAREIDS_STATIC)
    #define HARDWAREIDS_API
#elif defined(HARDWAREIDS_EXPORTS)
    #define HARDWAREIDS_API __declspec(dllexport)
#else
    #define HARDWAREIDS_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct HardwareIds_Options
{
    int ScanNeighborEndpoints;                  /* Non-zero: scan for the Wi-Fi networks around the computer. */
    int ScanLocalNetworkDevices;                /* Non-zero: scan the local networks for devices. */
    unsigned int DurationOfNetworkScanMs;       /* How long the Wi-Fi scan may take; 0 for the default (7000). */
    unsigned int DurationOfLocalNetworkScanMs;  /* How long devices have to answer the local network scan; 0 for the default (1000). */
    int Indented;                               /* Non-zero: indent the JSON (CRLF line breaks, two spaces). */
} HardwareIds_Options;

/*
 * Takes a snapshot of the local computer and returns it as a NUL-terminated JSON string (ASCII only), or NULL on failure.
 * InOptions may be NULL for the defaults (hardware only, compact JSON). Free the result with HardwareIds_Free.
 */
HARDWAREIDS_API char* HardwareIds_GetSnapshotJson(const HardwareIds_Options* InOptions);

/*
 * Frees a string returned by HardwareIds_GetSnapshotJson. Accepts NULL.
 */
HARDWAREIDS_API void HardwareIds_Free(char* InJson);

#ifdef __cplusplus
}
#endif

#endif /* HARDWAREIDS_H */
