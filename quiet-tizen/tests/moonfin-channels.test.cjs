'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('assert');
(async()=>{
 let receiver,current=null,calls=[],attributes={},fetches=0;
 const items=[{Id:'b',Name:'Two',ChannelNumber:'2'},{Id:'a',Name:'One',ChannelNumber:'1'},{Id:'c',Name:'Ten',ChannelNumber:'10'}];
 const win={addEventListener:()=>{},requestAnimationFrame:f=>setTimeout(f,0),__quietMoonfinChannels:{ready:()=>true,scope:{},sort:'number',list:()=>{fetches++;return Promise.resolve({Items:items})},current:()=>current,play:c=>{current=c.Id;calls.push(c.Id);return Promise.resolve()}}};
 const doc={hidden:false,documentElement:{setAttribute:(k,v)=>attributes[k]=v},querySelector:()=>null,addEventListener:()=>{}};
 const tizen={application:{getCurrentApplication:()=>({getRequestedAppControl:()=>null})},messageport:{requestLocalMessagePort:()=>({addMessagePortListener:f=>{receiver=f;return 1},removeMessagePortListener:()=>{}})}};
 vm.runInNewContext(fs.readFileSync(require('path').join(__dirname,'../launcher/moonfin/quiet-guide.js'),'utf8'),{window:win,document:doc,tizen,console:{log:()=>{}},Date,Promise,setTimeout,clearTimeout});
 function send(action,id){receiver([{key:'destination',value:action},{key:'request',value:id}])}
 async function done(){await new Promise(r=>setTimeout(r,30))}
 send('channel-up','1');await done();assert.equal(current,'a');
 send('channel-down','2');await done();assert.equal(current,'c');
 send('channel-up','3');await done();assert.equal(current,'a');
 send('channel-up','4');send('channel-up','5');await done();assert.equal(current,'c');
 send('channel-up','5');await done();assert.equal(calls.length,5);assert.equal(fetches,1);
 assert.equal(attributes['data-quiet-guide'],'channel-selected');
 console.log('Direct Moonfin channels: number ordering, both wrap boundaries, rapid presses, request deduplication and cached lineup passed.');
})().catch(e=>{console.error(e);process.exitCode=1});
