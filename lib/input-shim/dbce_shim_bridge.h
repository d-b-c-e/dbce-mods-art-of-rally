#pragma once
/* Additive experimental copied-input extension 1; base ABI 2 is unchanged.
 * No callbacks and no input-device ownership. All buffers are copied before
 * return. The source owner qualifies identities/caps and reads once per tick.
 * Source generations are globally unique/increasing for this process. QPC
 * microseconds use the system counter's origin, never a module-relative epoch.
 * Capture/control flags are refused until external provenance is versioned in
 * those formats. Existing ordinary ABI handles refuse bridge operations. */
#include "dbce_shim.h"
#pragma pack(push,8)
typedef struct dbce_shim_bridge_device_v1 {
    uint32_t struct_size,abi;uint64_t generation;
    uint8_t instance[16],product[16];
    uint32_t axis_mask,range_mask,pov_mask,flags;
    uint8_t button_mask[16];int32_t minimum[8],maximum[8];
} dbce_shim_bridge_device_v1;
typedef struct dbce_shim_bridge_sample_v1 {
    uint32_t struct_size,abi;uint64_t generation,sequence;
    int64_t read_start_us,read_end_us;int32_t poll_hresult,state_hresult;
    uint8_t poll_call,state_call,valid,reserved;int32_t axes[8];
    uint8_t buttons[128];uint32_t pov[4],reserved2;
} dbce_shim_bridge_sample_v1;
#pragma pack(pop)
#ifdef __cplusplus
extern "C" {
#endif
DBCE_SHIM_API int32_t dbce_shim_bridge_abi_version(void);
DBCE_SHIM_API int32_t dbce_shim_bridge_device_size(void);
DBCE_SHIM_API int32_t dbce_shim_bridge_sample_size(void);
DBCE_SHIM_API int32_t dbce_shim_bridge_open(const dbce_shim_open_args*,void** out,char* why,int32_t why_len);
DBCE_SHIM_API int32_t dbce_shim_bridge_devices(void*,const dbce_shim_bridge_device_v1*,int32_t count);
/* Optional additive export: publish metadata and exact owned interface paths in
 * one atomic batch. paths has count nonempty UTF-16 NUL-terminated strings,
 * each at most 259 code units. All are copied before return. The source owner
 * must query the same qualified generation; no name/product-only inference.
 * Old devices() remains available and publishes unavailable (empty) paths. */
DBCE_SHIM_API int32_t dbce_shim_bridge_devices_with_paths(void*,const dbce_shim_bridge_device_v1*,int32_t count,const wchar_t* const* paths);
DBCE_SHIM_API int32_t dbce_shim_bridge_tick(void*,const dbce_shim_bridge_sample_v1*,int32_t count,dbce_shim_frame*);
#ifdef __cplusplus
}
#endif
