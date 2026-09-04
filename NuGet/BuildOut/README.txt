Local NuGet feed for CommandBars.Package.

Keep this file so the feed directory exists in a fresh checkout. NuGet.config
registers this directory; PackageDemo consumes an exact pinned package version.

Follow ../../DESIGNER-SETUP.md from this directory for the full bootstrap:
build Server and Client before Package, set PackageDemo's version to the emitted
version, then force-restore and build PackageDemo. Building Package alone packs
existing assemblies and does not rebuild its source projects.

Versions have minute precision; repeated builds within one minute may share a
version. The package copy target also writes to E:\Nuget; see the setup guide
for this machine-specific requirement.

Retain versions referenced by consuming projects. Old packages may be removed
when no longer needed, but do not clear this feed as routine troubleshooting.
