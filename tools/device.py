#!/usr/bin/env python3
"""Explicit-target root/SDB transport. Private data stays below .local/."""
import argparse,asyncio,base64,hashlib,json,os,shlex,sys
from datetime import datetime,timezone
from pathlib import Path,PurePosixPath
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'quiet-tizen/vendor'))
from samsung_tv_root.archive_root import probe
from samsung_tv_root.sdb import SdbClient,find_sdb

def private(p):
 p=p.resolve()
 if '.local' not in p.parts:raise ValueError('Write private output below a .local directory')
 p.parent.mkdir(parents=True,exist_ok=True);return p

def main():
 a=argparse.ArgumentParser(description=__doc__);a.add_argument('--tv',required=True);a.add_argument('--pc',required=True);a.add_argument('--bridge-token-file',type=Path);a.add_argument('--payloads',type=Path,default=ROOT/'quiet-tizen/.local/build/payloads')
 s=a.add_subparsers(dest='action',required=True);s.add_parser('probe');e=s.add_parser('exec');e.add_argument('script',type=Path);e.add_argument('args',nargs='*');u=s.add_parser('push');u.add_argument('local',type=Path);u.add_argument('remote');d=s.add_parser('pull');d.add_argument('remote');d.add_argument('local',type=Path);x=s.add_parser('sdb');x.add_argument('args',nargs=argparse.REMAINDER);o=a.parse_args()
 token=o.bridge_token_file.read_text(encoding='ascii').strip() if o.bridge_token_file else None
 async def run(commands):
  r=await probe(o.tv,callback_host=o.pc,payloads=o.payloads,bridge_token=token,command_timeout=30,commands=tuple(commands))
  record=private(ROOT/'.local/command-records'/(datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'.json'));record.write_text(json.dumps(r,indent=2),encoding='utf-8')
  for c in r.get('commands',[]):
   if c['exit_code'] or c['timed_out']:raise RuntimeError('TV command failed; inspect state before retrying: '+c['stderr']+c['stdout'])
  if not r.get('system_files_unchanged') or not r.get('staging_cleaned'):raise RuntimeError('Root cleanup verification failed')
  return r
 if o.action in ('push','sdb'):
  c=SdbClient(find_sdb(),o.tv,bridge_token=token)
  try:
   c.connect();c.require_device()
   if o.action=='push':c.push(o.local.resolve(),PurePosixPath(o.remote));print('Pushed',o.local.name)
   else:r=c.run(('-s',c.serial,*o.args),check=True);print(r.stdout,end='')
  finally:c.disconnect()
  return
 if o.action=='pull':
  if not o.remote.startswith('/') or any(x in o.remote for x in ('\n','\0')):raise ValueError('Use an absolute single-line TV path')
  q=shlex.quote(o.remote);r=asyncio.run(run([f'stat -c %s {q};sha256sum {q}']))
  lines=r['commands'][0]['stdout'].splitlines();size=int(lines[0]);digest=lines[1].split()[0]
  if size>64*1024*1024:raise ValueError('Split files above 64 MiB')
  r=asyncio.run(run([f'dd if={q} bs=524288 skip={i} count=1 2>/dev/null | base64 -w0' for i in range((size+524287)//524288)]))
  data=b''.join(base64.b64decode(c['stdout'].strip(),validate=True) for c in r['commands'])
  if len(data)!=size or hashlib.sha256(data).hexdigest()!=digest:raise ValueError('Transfer hash/size mismatch')
  private(o.local).write_bytes(data);print('Verified private transfer:',size,'bytes');return
 commands=[]
 if o.action=='exec':
  script='set -- '+' '.join(shlex.quote(v) for v in o.args)+'\n'+o.script.read_text(encoding='utf-8-sig')
  cmd="printf %s '"+base64.b64encode(script.encode()).decode()+"' | base64 -d | /bin/sh"
  if len(cmd.encode())>65536:raise ValueError('Stage large scripts and pipe their contents into sh')
  commands=[cmd]
 r=asyncio.run(run(commands));path=private(ROOT/'.local/command-records'/(datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%f')+'.json'));path.write_text(json.dumps(r,indent=2),encoding='utf-8')
 for c in r.get('commands',[]):print(c['stdout'],end='')
 print('Root/cleanup verified. Private record:',path.relative_to(ROOT))
if __name__=='__main__':main()
