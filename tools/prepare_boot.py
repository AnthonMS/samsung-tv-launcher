#!/usr/bin/env python3
"""Build a private signed Q60C boot-helper package. Does not install it."""
import argparse,hashlib,io,json,os,shutil,subprocess,tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];Q=ROOT/'quiet-tizen';PKG='archive-root-00b0000000000021'
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--framework',type=Path,default=Q/'.local/tv-framework');p.add_argument('--output',type=Path,default=ROOT/'.local/boot');p.add_argument('--test-host');p.add_argument('--openssl',default='openssl');a=p.parse_args();out=a.output.resolve()
 if '.local' not in out.parts:raise ValueError('Output must be private below .local/')
 out.mkdir(parents=True,exist_ok=True,mode=0o700);out.chmod(0o700);stage=out/'package';stage.mkdir(exist_ok=True);res=stage/'resources';res.mkdir(exist_ok=True)
 def cmd(*args):subprocess.run(list(map(str,args)),check=True,capture_output=True)
 for project,dest in [('BootPrivacy.csproj','helper'),('Bridge.csproj','bridge')]:cmd('dotnet','build',Q/'tools/boot-privacy'/project,'-c','Release','-o',out/dest,f'-p:TV_FRAMEWORK_DIR={a.framework.resolve()}')
 for name in ('com.samsung.tv.ghservice.dll','com.samsung.tv.ghservice.runtimeconfig.json'):shutil.copy2(out/'helper'/name,stage/name)
 shutil.copy2(out/'bridge/QuietPrivacyBridge.dll',stage/'QuietPrivacyBridge.dll')
 key=out/'signing-private.pem';pub=res/'public.pem'
 if not key.exists():cmd(a.openssl,'genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:2048','-out',key)
 cmd(a.openssl,'pkey','-in',key,'-pubout','-out',pub)
 (res/'passwd').write_bytes(b'owner:x:0:0:owner:/home/owner:/bin/sh\n')
 shutil.copy2(Q/'.local/build/payloads/SamsungTvArchiveRoot.dll',res/'SamsungTvArchiveRoot.dll')
 (res/'SamsungTvArchiveRoot.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net6.0','framework':{'name':'Microsoft.NETCore.App','version':'6.0.0'},'rollForward':'Major'}}),encoding='ascii')
 data=(Q/'tools/boot-privacy/apply.sh').read_bytes();archive=res/(PKG+'.tar.gz')
 with tarfile.open(archive,'w:gz') as t:
  m=tarfile.TarInfo('launch.sh');m.size=len(data);m.mode=0o600;t.addfile(m,io.BytesIO(data))
 sig=Path(str(archive)+'.signature');cmd(a.openssl,'dgst','-sha256','-sign',key,'-out',sig,archive);cmd(a.openssl,'dgst','-sha256','-verify',pub,'-signature',sig,archive)
 token=out/'bridge.token'
 if not token.exists():
  import secrets;token.write_text(secrets.token_hex(16)+'\n',encoding='ascii')
 token.chmod(0o600);key.chmod(0o600);shutil.copy2(token,stage/'bridge.token')
 endpoint=res/'test-endpoint'
 if a.test_host:endpoint.write_text(a.test_host+'\n27721\n',encoding='ascii')
 else:endpoint.unlink(missing_ok=True)
 manifest=a.manifest.read_bytes()
 if manifest.count(b'on-boot="false"')!=1:raise ValueError('Inspect this manifest before adapting it')
 (stage/'manifest.xml').write_bytes(manifest.replace(b'on-boot="false"',b'on-boot="true"'))
 target=out/'boot-package.tar.gz'
 with tarfile.open(target,'w:gz') as t:
  for f in stage.iterdir():t.add(f,arcname=f.name)
 print('Private package:',target,'SHA256',hashlib.sha256(target.read_bytes()).hexdigest());print('Keep the signing key and bridge.token private. No TV changes made.')
if __name__=='__main__':main()
