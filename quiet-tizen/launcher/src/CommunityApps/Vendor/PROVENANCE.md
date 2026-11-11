# Signing implementation provenance

TizenResigner.cs is adapted from https://github.com/PatrickSt1991/tizen-sdb at commit b760156e74949e9033e411d433f627314ef0e4ce. Source: src/TizenSdb.Core/SigningManager/TizenWgtSigner.cs. MIT license retained in LICENSE.tizen-sdb.txt. Only the package signature implementation and canonicalization helper are included, with explicit usings and local namespace. Certificate loading, package policy and installation are implemented by Quiet Home. No TV-Store code is copied.
