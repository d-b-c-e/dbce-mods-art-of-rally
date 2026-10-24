#pragma once
/* dbce_shim: the shared input shim (windows/shim Core + DInputProvider, windows/host ProviderHost) behind an experimental
   C ABI for managed game adapters (Astra 16982/17050/18002). ABI version 2 (version 1's calls unchanged, the latched
   control and capture seams added): not a stable-ABI promise.
   - The handle belongs to the thread that opened it (the game's input thread). Every call taking a handle must be made on
     that thread; dbce_shim_abi_version, dbce_shim_frame_size and dbce_shim_action_name may be called from any thread.
   - No callbacks. Text is UTF-8 except the folder (UTF-16). Buffers are caller-owned and bounded: text is truncated,
     always NUL-terminated, and `needed` (when given) receives the full length plus one.
   - Errors are bounded result codes (below); nothing throws across the boundary.
   - Ownership follows the host's P21 lifecycle: dbce_shim_stop runs the bounded stop on the input thread. A resolved stop
     frees the host; an unresolved one leaves the host and its workers alive until process exit (never torn down under
     a running worker). Either way the handle is spent: later calls return DBCE_SHIM_STOPPED.
   - The shim reads devices non-exclusively and never creates force effects. dbce_shim_open is ordinary (no control
     worker, no capture). dbce_shim_open2 adds a latched test session (the reviewed control worker and journal, admitted
     only on the caller's verified fences and process-latched force mute) and a capture (the shared frame recorder: the
     Core frame plus the game's own channels, sealed out of process under P21). */
#include <stdint.h>
#ifdef DBCE_SHIM_BUILD
#define DBCE_SHIM_API __declspec(dllexport)
#else
#define DBCE_SHIM_API __declspec(dllimport)
#endif
#ifdef __cplusplus
extern "C" {
#endif

#define DBCE_SHIM_ABI 2
#define DBCE_SHIM_ACTIONS 31

enum {
    DBCE_SHIM_OK = 0,
    DBCE_SHIM_UNRESOLVED = 1,      /* dbce_shim_stop: the host did not resolve; it stays alive until process exit */
    DBCE_SHIM_BAD_ARGUMENT = -1,   /* null, wrong size, bad handle or a value out of range */
    DBCE_SHIM_WRONG_THREAD = -2,   /* not the thread that opened the handle */
    DBCE_SHIM_REFUSED = -3,        /* the host or its devices refused; see the why buffer */
    DBCE_SHIM_STOPPED = -4,        /* the handle was stopped */
    DBCE_SHIM_STALE = -5           /* dbce_shim_record: not the pending frame, or it was already recorded */
};

enum { DBCE_SHIM_LATCHED = 1, DBCE_SHIM_CAPTURE = 2 };   /* dbce_shim_open_args.flags */

/* One action of a Core frame. status is dbce::shim::Status, value is normalized (steer -1..1, others 0..1). 8 bytes. */
typedef struct dbce_shim_action {
    float value;
    uint8_t status;
    uint8_t injected;
    uint8_t reserved[2];
} dbce_shim_action;

/* One Core frame: what a game adapter consumes. Every field is naturally aligned; 296 bytes (asserted). */
typedef struct dbce_shim_frame {
    uint32_t struct_size;          /* in: sizeof(dbce_shim_frame) as the caller allocated it; must equal dbce_shim_frame_size() */
    uint32_t abi;                  /* out: DBCE_SHIM_ABI */
    uint64_t seq;
    int64_t tick_us;
    uint64_t down, up;             /* digital edges against the previous frame, bit = action index */
    uint8_t run;                   /* dbce::shim::Run */
    uint8_t latched;
    uint8_t suppress_native;
    uint8_t reserved0;
    uint32_t sample_count;
    dbce_shim_action action[DBCE_SHIM_ACTIONS];
} dbce_shim_frame;

/* dbce_shim_open2's arguments. Fixed layout, naturally aligned, 128 bytes (asserted). Strings are UTF-8 (paths
   UTF-16); fields a flag does not use must be zero/null. */
typedef struct dbce_shim_open_args {
    uint32_t struct_size;          /* sizeof(dbce_shim_open_args); must equal dbce_shim_open_args_size() */
    uint32_t flags;                /* DBCE_SHIM_LATCHED | DBCE_SHIM_CAPTURE */
    int32_t profile_len;
    uint8_t fences_verified;       /* LATCHED: the entry point verified its run fences (1), as OutRun's fence does */
    uint8_t force_absent;          /* LATCHED: physical force is latched off for the process (1) */
    uint8_t reserved[2];
    const wchar_t* folder;         /* an existing run folder: the host's report and, latched, the control journal */
    const char* nonce;             /* LATCHED: the session nonce the control worker requires */
    const char* profile;           /* the exact [Controls] bytes */
    const char* unsupported;       /* "action=reason" lines, or null */
    void* hwnd;
    const wchar_t* session_file;   /* LATCHED: the test session (inject.on) */
    const wchar_t* batch_file;     /* LATCHED: the action batches (inject.txt) */
    const wchar_t* capture_folder; /* CAPTURE: created new by the first row, inside the run folder */
    const char* game_id;           /* CAPTURE: [a-z0-9.-], 1-64: the metadata game and bundle.meta's game= */
    const char* game_channels;     /* CAPTURE: "\n"-separated names, recorded as "game.<name>", plus "game.valid" */
    const char* context;           /* CAPTURE: free text for the metadata */
    const char* adapter_version;   /* the journal's and the capture's adapter version */
    double hz, seconds;            /* CAPTURE: the worst-case preflight (rows per second, duration) */
} dbce_shim_open_args;

DBCE_SHIM_API int32_t dbce_shim_abi_version(void);
DBCE_SHIM_API int32_t dbce_shim_frame_size(void);
DBCE_SHIM_API int32_t dbce_shim_open_args_size(void);
/* The standard action id at index 0..DBCE_SHIM_ACTIONS-1 (a static string), or null. */
DBCE_SHIM_API const char* dbce_shim_action_name(int32_t index);

/* Opens an ordinary host. folder: an existing folder for the host's own report (host-shutdown.json). profile: the exact
   [Controls] bytes (ini-controls-1). unsupported: "action=reason" lines naming actions the game cannot use (may be null).
   hwnd: the game window (may be null). On success *out is the handle. */
DBCE_SHIM_API int32_t dbce_shim_open(const wchar_t* folder, const char* nonce, const char* profile, int32_t profile_len,
                                     const char* unsupported, void* hwnd, void** out, char* why, int32_t why_len);
/* Opens a host as the arguments say (version 2). A latched open is refused unless fences_verified and force_absent are
   both 1 and both session files are named. */
DBCE_SHIM_API int32_t dbce_shim_open2(const dbce_shim_open_args* args, void** out, char* why, int32_t why_len);
/* One Core tick: fills *frame (frame->struct_size must be set). With a capture, it first pushes the previous frame's row. */
DBCE_SHIM_API int32_t dbce_shim_tick(void* handle, dbce_shim_frame* frame);
/* The start report (line, then notes, one per line) and the device table (JSON array), as text.
   Device rows include optional identity evidence: interfacePath is an exact UTF-8 owned-handle path, or "" when
   unavailable/not in an adopted open epoch. Query after tick; never match a device by an absent or previous path. */
DBCE_SHIM_API int32_t dbce_shim_start_report(void* handle, char* text, int32_t len, int32_t* needed);
DBCE_SHIM_API int32_t dbce_shim_device_reports(void* handle, char* text, int32_t len, int32_t* needed);
/* The profile as Core's own parser read it, "key=value" lines: ok (1/0), error, profile, revision, transmission and
   the SHA-256 of the exact bytes. A game adapter takes its gearbox from transmission, never from its own parse. */
DBCE_SHIM_API int32_t dbce_shim_profile_info(void* handle, char* text, int32_t len, int32_t* needed);
/* OPTIONAL extension; ABI 2 alone does not promise this export. Pure inspection of the exact profile bytes (at most
   64 KiB), using Core's parser and binding admission. No host, worker, provider, files, device or force calls.
   action is a dbce_shim_action_name index. JSON schema 1: action, status (bound/unbound/invalid), reason, sha256,
   kind, instance, product and name. Identity fields are empty unless the entire profile has no invalid/conflicting
   actions and the requested action is bound. This is configured identity, NOT evidence of connected hardware.
   The usual text/needed contract applies; callers must reject truncation. May be called from any thread. */
DBCE_SHIM_API int32_t dbce_shim_profile_action_info(const char* profile, int32_t profile_len, int32_t action,
                                                 char* text, int32_t len, int32_t* needed);
/* CAPTURE: the game's channel values (in game_channels order) for the frame just ticked, frame_seq its seq. Exactly
   once per frame: another frame's seq, a second call or no pending frame gives DBCE_SHIM_STALE and changes nothing.
   A wrong count or a non-finite value gives DBCE_SHIM_BAD_ARGUMENT and the row is recorded with game.valid=0. A frame
   that gets no call is recorded with game.valid=0 and zeros: a missing value is visible, never reused or invented. */
DBCE_SHIM_API int32_t dbce_shim_record(void* handle, uint64_t frame_seq, const double* values, int32_t count);
/* The host's bounded stop. OK when resolved, DBCE_SHIM_UNRESOLVED otherwise; report receives the reasons. With a capture
   it first pushes the pending row; latched, it then closes control admission and ticks Core once more (that final frame
   recorded with game.valid=0: the caller never consumed it), before the host's own stop. */
DBCE_SHIM_API int32_t dbce_shim_stop(void* handle, char* report, int32_t len, int32_t* needed);

#ifdef __cplusplus
}
#endif
