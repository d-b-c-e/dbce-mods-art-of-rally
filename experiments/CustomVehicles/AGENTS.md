# Custom vehicle framework experiment

The owner explicitly authorized sustained development and peer review on 2026-10-08,
including real custom vehicle definitions. This is separate from the accepted cosmetic
Turtle Van. Managed physics experiments here are within that authorization; the root
no-handling-change rule still applies to the shipped Art of Sim Rally product.

- Preserve the installed TurtleVan 0.1.4 and core mod/config/FFB baseline while the owner
  is away. Build/package offline; no game, input, wheel-force or rig launch.
- Keep changes in this directory, except coordinated documentation of STD-018 evidence.
- Custom handling is default-off per launch, applies only to a selected player instance
  in Free Roam, and requires installed/verified upload guards. Never mutate prefab assets.
- Online upload suppression latches until process exit after any physical override;
  disabling the feature or changing vehicle does not clear it. No unpatch while tainted.
- Keep native vehicle identifiers in native saves. Store custom ids in mod-owned data.
- No game assemblies/decompiled sources/binaries in git. Local research is ignored.
- Keep Blender/mesh, managed tests, build and attended runtime evidence distinct.
- Use hcom tara to coordinate read-only reviews with hula (Astra) and nene (Claude).
