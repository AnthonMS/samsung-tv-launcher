'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const script=fs.readFileSync(require('path').join(__dirname,'../launcher/moonfin/quiet-guide.js'),'utf8');
function env(initial){
 let time=1000,id=0,requested=initial,pending=new Map(),events={},status=[],calls=0,ready=false,guide=false;
 const doc={hidden:false,documentElement:{setAttribute:(k,v)=>status.push(v)},querySelector:()=>guide?{}:null,addEventListener:(n,f)=>events[n]=f};
 const win={addEventListener:(n,f)=>events[n]=f};
 const context={window:win,document:doc,console:{log:()=>{}},Date:{now:()=>time},tizen:{messageport:{requestLocalMessagePort:()=>({addMessagePortListener:f=>{events.messageport=f;return 1},removeMessagePortListener:()=>{}})},application:{getCurrentApplication:()=>({getRequestedAppControl:()=>requested})}},setTimeout:(f,ms)=>{pending.set(++id,{f,at:time+ms});return id},clearTimeout:i=>pending.delete(i)};
 function advance(ms){const end=time+ms;for(;;){let entry=[...pending].sort((a,b)=>a[1].at-b[1].at)[0];if(!entry||entry[1].at>end)break;time=entry[1].at;pending.delete(entry[0]);entry[1].f();}time=end;}
 vm.runInNewContext(script,context);
 return {doc,win,events,status,pending,advance,setRequest:r=>requested=r,setGuide:b=>guide=b,setReady:b=>ready=b,install:()=>win.__quietMoonfinOpenGuide=()=>{if(!ready)return false;calls++;return true},calls:()=>calls};
}
const request={appControl:{data:[{key:'quiet_destination',value:['guide']}]}};
let e=env(null);assert.equal(e.pending.size,0);e.install();e.setReady(true);e.setRequest(request);e.events.appcontrol();assert.equal(e.calls(),1);e.setGuide(true);e.advance(100);assert.equal(e.status.at(-1),'opened');assert.equal(e.pending.size,0);
e.advance(400);e.setGuide(false);e.events.appcontrol();assert.equal(e.calls(),2);e.doc.hidden=true;e.events.visibilitychange();assert.equal(e.status.at(-1),'cancelled');e.advance(26000);assert.equal(e.calls(),2);
e=env(request);e.install();e.advance(500);assert.equal(e.calls(),0);e.setReady(true);e.setGuide(true);e.advance(100);assert.equal(e.calls(),1);assert.equal(e.status.at(-1),'opened');
e=env(request);e.advance(25100);assert.equal(e.status.at(-1),'timed-out');assert.equal(e.pending.size,0);
e=env(null);e.install();e.setReady(true);e.setGuide(true);e.events.messageport([{key:'destination',value:'guide'}]);assert.equal(e.calls(),1);assert.equal(e.status.at(-1),'opened');e.advance(400);e.events.messageport([{key:'destination',value:'other'}]);assert.equal(e.calls(),1);
console.log('Moonfin guide: cold launch, warm message port, readiness, repeat, cancellation and timeout passed.');
