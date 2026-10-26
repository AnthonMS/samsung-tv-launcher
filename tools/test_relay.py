#!/usr/bin/env python3
"""Temporary one-client relay for a pre-transition headless-root test."""
import argparse,socket,threading
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--tv',required=True);p.add_argument('--port',type=int,default=27721);a=p.parse_args()
s=socket.socket();s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);s.bind(('0.0.0.0',a.port));s.listen(1);s.settimeout(180);print('Relay ready; only the explicitly selected TV may connect.',flush=True)
c,peer=s.accept()
if peer[0]!=a.tv:c.close();s.close();raise SystemExit('Unexpected client rejected')
u=socket.create_connection((a.tv,26101),timeout=10)
def pump(x,y):
 try:
  while True:
   b=x.recv(65536)
   if not b:break
   y.sendall(b)
 except OSError:pass
 finally:
  try:y.shutdown(socket.SHUT_WR)
  except OSError:pass
t=threading.Thread(target=pump,args=(c,u));t.start();pump(u,c);t.join(30);c.close();u.close();s.close();print('Relay finished; it is not a persistent PC service.')
