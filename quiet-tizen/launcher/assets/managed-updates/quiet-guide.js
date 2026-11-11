/* Quiet Home guide shortcut. Reuses Moonfin's navigation and player cleanup. */
(function () {
 'use strict';
 var timer = null, generation = 0, lastRequest = -1000, visible = false, port = null, watch = null;
 function status(value) {
  document.documentElement.setAttribute('data-quiet-guide', value);
  console.log('[QuietGuide] ' + value);
 }
 function stop() { generation++; if (timer !== null) clearTimeout(timer); timer = null; }
 function request() {
  var now = Date.now();
  if (now - lastRequest < 300) return;
  lastRequest = now; stop();
  var mine = generation, deadline = now + 25000, dispatched = false;
  visible = !document.hidden; status('requested');
  function tick() {
   timer = null;
   if (mine !== generation) return;
   if (Date.now() > deadline) { status('timed-out'); stop(); return; }
   if (!document.hidden) {
    visible = true;
    if (!dispatched && typeof window.__quietMoonfinOpenGuide === 'function') {
     try { dispatched = window.__quietMoonfinOpenGuide() === true; }
     catch (ignore) { status('failed'); stop(); return; }
     if (dispatched) status('dispatched');
    }
    if (dispatched && document.querySelector('[data-spotlight-id="livetv-windowbar"]')) {
     status('opened'); stop(); return;
    }
   }
   timer = setTimeout(tick, 100);
  }
  tick();
 }

 var channelQueue = Promise.resolve(), channelGeneration = 0, channelVisible = false;
 var channelScope = null, channelList = [], channelAt = 0, channelCursor = null;
 var seen = [], lastChannelRequest = '';
 function byName(a,b) { var x=(a.Name||'').toLowerCase(),y=(b.Name||'').toLowerCase();return x<y?-1:x>y?1:0; }
 function byNumber(a,b) {
  function parts(x) {var s=String(x==null?'':x).trim();return /^[+-]?\d+(\.[+-]?\d+)*$/.test(s)?s.split('.').map(Number):null;}
  var x=parts(a.ChannelNumber),y=parts(b.ChannelNumber);
  if(!x||!y)return x?-1:y?1:byName(a,b);
  for(var i=0;i<Math.max(x.length,y.length);i++){var n=(x[i]||0)-(y[i]||0);if(n)return n;}
  return byName(a,b);
 }
 function channels(action,id) {
  if(id&&seen.indexOf(id)>=0)return;
  if(id){seen.push(id);if(seen.length>64)seen.shift();}
  var mine=channelGeneration,deadline=Date.now()+25000;
  channelQueue=channelQueue.catch(function(){}).then(function(){
   return new Promise(function(resolve,reject){
    function ready(){
     if(mine!==channelGeneration)return reject(new Error('cancelled'));
     if(Date.now()>deadline)return reject(new Error('timeout'));
     var api=window.__quietMoonfinChannels;
     if(document.hidden||!api||!api.ready()){setTimeout(ready,100);return;}
     channelVisible=true;resolve(api);
    }
    ready();
   });
  }).then(function(api){
   if(channelScope!==api.scope||Date.now()-channelAt>120000){channelScope=api.scope;channelCursor=null;return Promise.resolve(api.list()).then(function(data){channelList=(data&&data.Items||[]).slice();channelAt=Date.now();return api;});}
   return api;
  }).then(function(api){
   if(mine!==channelGeneration)throw new Error('cancelled');
   var list=channelList.slice().sort(function(a,b){if(api.sort==='name')return byName(a,b);if(api.sort==='favoritesFirst'){var x=!!(a.UserData&&a.UserData.IsFavorite),y=!!(b.UserData&&b.UserData.IsFavorite);if(x!==y)return x?-1:1;}return byNumber(a,b);});
   if(!list.length)throw new Error('no-channels');
   var current=api.current(),index=-1;
   for(var i=0;i<list.length;i++)if(list[i].Id===(current||channelCursor))index=i;
   var target=list[index<0?(action==='channel-up'?0:list.length-1):(index+(action==='channel-up'?1:-1)+list.length)%list.length];
   return Promise.resolve(api.play(target,list)).then(function(){channelCursor=target.Id;document.documentElement.setAttribute('data-quiet-channel',target.Name||target.Id);status('channel-selected');return new Promise(function(resolve){window.requestAnimationFrame(function(){window.requestAnimationFrame(resolve);});});});
  }).catch(function(error){if(error.message!=='cancelled')status('channel-failed');});
 }
 function incoming(action,id){
  if(action==='guide'){channelGeneration++;request();}
  else if(action==='channel-up'||action==='channel-down')channels(action,id);
 }
 function readRequest() {
  try {
   var current = tizen.application.getCurrentApplication().getRequestedAppControl();
   var data = current && current.appControl && current.appControl.data || [];
   var action='',id='';
   for(var i=0;i<data.length;i++){if(data[i].key==='quiet_destination')action=data[i].value[0];if(data[i].key==='quiet_request')id=data[i].value[0];}
   incoming(action,id);
  } catch (ignore) { /* Ordinary app launches have no requested app control. */ }
 }
 window.addEventListener('appcontrol', readRequest);
 try {
  port = tizen.messageport.requestLocalMessagePort('quiet_guide');
  watch = port.addMessagePortListener(function (data) {
   var action='',id='';for(var i=0;i<data.length;i++){if(data[i].key==='destination')action=data[i].value;if(data[i].key==='request')id=data[i].value;}
   incoming(action,id);
  });
 } catch (ignore) { /* Cold app-control handling remains available. */ }
 document.addEventListener('visibilitychange', function () {
  if(channelVisible&&document.hidden){channelGeneration++;channelVisible=false;}
  if (visible && document.hidden && timer !== null) { status('cancelled'); stop(); }
 });
 window.addEventListener('pagehide', function () {
  stop();
  if (port !== null && watch !== null) try { port.removeMessagePortListener(watch); } catch (ignore) {}
 });
 readRequest();
}());
