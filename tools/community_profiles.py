#!/usr/bin/env python3
"""Prepare private Quiet Home signing profiles locally. Does not contact a TV.

Each input folder contains author.p12, distributor.p12 and password.txt.
The output must be in ignored .local storage. Never publish it.
Requires Python cryptography. No Apps2Samsung installation is required.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil


def prepare(directories, destination):
    from cryptography.hazmat.primitives.serialization.pkcs12 import load_key_and_certificates
    from cryptography.hazmat.primitives.serialization import Encoding
    destination = destination.resolve()
    if '.local' not in destination.parts:
        raise ValueError('Keep signing material under an ignored .local directory')
    if destination.exists():
        raise ValueError('Refusing to overwrite an existing signing bundle')
    profiles = []
    staged = []
    now = datetime.now(timezone.utc)
    for folder in directories:
        folder = folder.resolve()
        password = (folder / 'password.txt').read_text(encoding='utf-8-sig').strip()
        certificates = []
        for name in ('author.p12', 'distributor.p12'):
            key, certificate, chain = load_key_and_certificates((folder / name).read_bytes(), password.encode())
            if key is None or certificate is None:
                raise ValueError('A profile is missing its private key or certificate')
            if not certificate.not_valid_before_utc <= now <= certificate.not_valid_after_utc:
                raise ValueError('A signing certificate is expired or not valid yet')
            certificates.append(certificate)
        fingerprint = hashlib.sha256(certificates[0].public_bytes(Encoding.DER)).hexdigest()
        identity = fingerprint[:16] + '-' + hashlib.sha256(certificates[1].public_bytes(Encoding.DER)).hexdigest()[:8]
        level = 'partner' if 'partner' in folder.name.lower() else 'public'
        if not any(x['Id'] == identity for x in profiles):
            profiles.append(dict(Id=identity, Name=folder.name, AuthorFile=identity+'/author.p12',
                                 DistributorFile=identity+'/distributor.p12', AuthorPassword=password,
                                 DistributorPassword=password, Level=level))
            staged.append((folder, identity))
    if not profiles:
        raise ValueError('At least one signing profile is required')
    destination.mkdir(parents=True, mode=0o700)
    for folder, identity in staged:
        target = destination / identity
        target.mkdir(mode=0o700)
        for name in ('author.p12', 'distributor.p12'):
            shutil.copyfile(folder/name, target/name)
            (target/name).chmod(0o600)
    profiles.sort(key=lambda x: x['Level'] == 'partner')
    path = destination/'profiles.json'
    path.write_text(json.dumps(profiles, indent=2), encoding='utf-8')
    path.chmod(0o600)
    return len(profiles)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--profile-dir', type=Path, action='append', required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    try:
        count = prepare(args.profile_dir, args.output)
    except Exception:
        p.exit(1, 'Could not prepare profiles. Check paths, certificate validity and passwords. No private values were logged.\n')
    print(f'Prepared {count} private signing profiles. Keep the entire output private.')


if __name__ == '__main__':
    main()
