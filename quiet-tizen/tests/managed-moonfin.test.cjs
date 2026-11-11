'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');let checks=0; const fixtures=process.argv[2]; if(!fixtures)throw new Error('Supply the upstream fixture directory used by ManagedUpdateTests.');
(async()=>{
for(const v of ['2.10.0','2.11.0','3.0.0']){
 const s=fs.readFileSync(path.join(fixtures,'adapted-'+v+'.js'),'utf8');const start=s.indexOf('notifyInteraction:(window.__quietMoonfinOpenGuide=')+'notifyInteraction:'.length;const end=s.indexOf(',wakeScreensaver:',start);assert(start>30&&end>start);
 const old=v==='2.10.0';const names=old?['hi','qo','je','py','Lt','l','y','ae','Ro','yo','to']:['Sa','Vi','Re','iS','Vt','l','b','se','Di','wi','Yo'];const [clear,guide,panel,panels,current,api,settings,cleanup,next,play,interaction]=names;const events=[];
 const c={window:{},Promise};c[panels]={LOGIN:'login',ADD_SERVER:'server',ADD_USER:'user',PLAYER:'player'};c[panel]='home';c[current]={Id:'channel-one'};c[clear]=()=>events.push('clear');c[guide]=()=>events.push('guide');c[interaction]=()=>{};c[next]=x=>events.push('next:'+x.Id);c[play]=(x,list)=>events.push('play:'+x.Id+':'+list.length);c[api]={getLiveTvChannels:()=>Promise.resolve({Items:[{Id:'channel-two'}]})};c[settings]={liveTvChannelSortBy:'name'};c[cleanup]={D0:()=>{events.push('release');return Promise.resolve();}};
 vm.runInNewContext(s.slice(start,end),c);const bridge=c.window.__quietMoonfinChannels;
 assert(c.window.__quietMoonfinOpenGuide());assert.deepStrictEqual(events.splice(0),['clear','guide']);checks++;
 assert(bridge.ready());assert.equal(bridge.current(),'channel-one');assert.equal(bridge.sort,'name');checks++;
 await bridge.play({Id:'channel-two'},[{Id:'channel-two'}]);assert.deepStrictEqual(events.splice(0),['clear','play:channel-two:1']);checks++;
 c[panel]='player';await bridge.play({Id:'channel-three'},[]);assert.deepStrictEqual(events.splice(0),['clear','release','next:channel-three']);checks++;
 c[panel]='login';assert.equal(bridge.ready(),false);assert.equal(c.window.__quietMoonfinOpenGuide(),false);assert.equal(events.length,0);checks++;
 assert.equal((await bridge.list()).Items[0].Id,'channel-two');checks++;
}
console.log(checks+' generated Moonfin callback execution checks passed');
})().catch(e=>{console.error(e);process.exitCode=1;});
