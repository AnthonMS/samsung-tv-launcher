'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');const fixtures=process.argv[2];let checks=0;
(async()=>{
for(const version of ['2.11.0','3.0.0']){
 const s=fs.readFileSync(path.join(fixtures,'adapted-player-'+version+'.js'),'utf8');const start=s.indexOf('seek:function(e){Eu(),Fa.current');assert(start>0);const end=s.indexOf('},next:',start)+1;const expression='('+s.slice(start+'seek:'.length,end)+')';
 const reports=[],events=[];let complete,reject,actual=120128,state='PAUSED';const c={Math,Promise,Eu:()=>events.push('scrub-cleared'),Fa:{current:true},eu:()=>false,Na:{current:2640000000},Ne:{warn:()=>events.push('failure')},f:{wX:(ticks,options)=>{reports.push({ticks,options});return Promise.resolve();}},v:{avplaySeek:ms=>{events.push(ms);return new Promise((r,j)=>{complete=r;reject=j;});},avplayGetCurrentTime:()=>actual,avplayGetState:()=>state}};
 const seek=vm.runInNewContext(expression,c);seek(1200000000);assert.equal(reports.length,0);checks++;complete();await new Promise(setImmediate);assert.equal(reports.length,1);assert.equal(reports[0].ticks,1201280000);assert.equal(c.Na.current,1201280000);assert.equal(reports[0].options.isPaused,true);checks++;
 state='PLAYING';actual=180123;seek(1800000000);complete();await new Promise(setImmediate);assert.equal(reports[1].options.isPaused,false);assert.equal(reports[1].ticks,1801230000);checks++;
 seek(2000000000);reject(new Error('not seekable'));await new Promise(setImmediate);assert.equal(reports.length,2);assert(events.includes('failure'));checks++;
 c.eu=()=>true;seek(2200000000);await new Promise(setImmediate);assert.equal(reports.length,2);checks++;
}
console.log(checks+' actual-completion seek reporting checks passed');
})().catch(e=>{console.error(e);process.exitCode=1;});
