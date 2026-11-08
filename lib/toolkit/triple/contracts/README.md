# Triple-screen interchange contracts

These v1 schemas define the files exchanged by the optimizer and game adapters:

- `triple-screen-layout.schema.json`: measured rig and requested output topology.
- `adapter-manifest.schema.json`: identity, supported games/topologies/capabilities, and owned files.
- `runtime-status.schema.json`: accepted layout hash and observed rendering state.

The optimizer writes desired state; the mod validates it and publishes observed status. A matching
layout hash alone is not proof of three active cameras. Consumers must also check active capability
and frame evidence. Writers use atomic replacement; readers reject unsupported schema versions.

Camera behavior and the shared F7 comparison follow the
[camera framing policy, revision 2](../docs/camera-framing-and-comparison-policy.md).
This is a consumer behavior policy; it does not change the v1 JSON schemas.
