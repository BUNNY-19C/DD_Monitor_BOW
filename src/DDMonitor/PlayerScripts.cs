using System.Globalization;

namespace DDMonitor;

internal static class PlayerScripts
{
    public static string Audio(int percent) => """
    (() => {
      if (location.hostname !== 'live.bilibili.com') return;
      let level = __VOLUME__;
      let muted = null;
      let notified = false;
      function apply(video) {
        if (!(video instanceof HTMLMediaElement)) return;
        if (Math.abs(video.volume - level) > 0.0001) video.volume = level;
        if (muted !== null && video.muted !== muted) video.muted = muted;
        if (!notified) {notified=true;window.chrome?.webview?.postMessage({kind:'dd-audio-ready'});}
      }
      const scan = () => document.querySelectorAll('video,audio').forEach(apply);
      window.__ddAudio = {
        setVolume(value) {level=Math.max(0,Math.min(1,value));scan();},
        setMuted(value) {muted=!!value;scan();}
      };
      for (const event of ['play','loadedmetadata','volumechange'])
        document.addEventListener(event,e=>apply(e.target),true);
      function observe() {
        new MutationObserver(records=>{
          for (const record of records) for (const node of record.addedNodes) {
            if (!(node instanceof Element)) continue;
            if (node.matches('video,audio')) apply(node);
            node.querySelectorAll('video,audio').forEach(apply);
          }
        }).observe(document.documentElement,{childList:true,subtree:true});
        scan();
      }
      if (document.documentElement) observe();
      else document.addEventListener('DOMContentLoaded',observe,{once:true});
    })();
    """.Replace("__VOLUME__", (Math.Clamp(percent, 0, 100) / 100.0).ToString(CultureInfo.InvariantCulture));

    public const string Compact = """
    (() => {
      if (location.hostname !== 'live.bilibili.com') return;
      function style() {
        if (!document.documentElement || document.getElementById('dd-compact-style')) return;
        const sheet=document.createElement('style');sheet.id='dd-compact-style';
        sheet.textContent=`
          html:not(.dd-compact) ::-webkit-scrollbar {width:8px;height:8px;background:#171a21;}
          html:not(.dd-compact) ::-webkit-scrollbar-thumb {background:#536171;border-radius:4px;}
          html.dd-compact,html.dd-compact body {width:100%!important;height:100%!important;min-width:0!important;margin:0!important;overflow:hidden!important;background:#000!important;}
          html.dd-compact::-webkit-scrollbar,html.dd-compact *::-webkit-scrollbar {display:none!important;}
          html.dd-compact .dd-player-path > :not(.dd-player-path):not(.player-section) {display:none!important;}
          html.dd-compact #fullscreen-container {position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;min-width:0!important;padding:0!important;margin:0!important;border:0!important;box-sizing:border-box!important;background:#000!important;background-image:none!important;--first-frame-bg:none!important;overflow:hidden!important;z-index:2147483000!important;}
          html.dd-compact #fullscreen-container::before,html.dd-compact #fullscreen-container::after {display:none!important;}
          html.dd-compact #fullscreen-container > :not(.player-section) {display:none!important;}
          html.dd-compact .web-player-danmaku,html.dd-compact .web-player-inject-wrap {display:none!important;}
          html.dd-compact .player-section,html.dd-compact #live-player-ctnr,html.dd-compact #live-player {width:100%!important;height:100%!important;min-height:0!important;margin:0!important;padding:0!important;}
          html.dd-compact #fullscreen-container > .player-section {position:absolute!important;inset:0!important;height:100vh!important;min-height:0!important;max-height:none!important;}
          html.dd-compact #fullscreen-container > .player-section > .h-100 {height:100%!important;}
          html.dd-compact #fullscreen-container > .player-section,html.dd-compact #live-player-ctnr,html.dd-compact #live-player {background:#000!important;overflow:hidden!important;}
          html.dd-compact video {width:100%!important;height:100%!important;object-fit:contain!important;background:#000!important;}
        `;
        document.documentElement.appendChild(sheet);
      }
      function path() {let node=document.getElementById('fullscreen-container');if(!node)return false;for(;node;node=node.parentElement)node.classList.add('dd-player-path');return true;}
      window.__ddView={setCompact(value){style();path();document.documentElement.classList.toggle('dd-compact',value);window.dispatchEvent(new Event('resize'));}};
      function enable(){style();document.documentElement.classList.add('dd-compact');path();}
      if(document.documentElement) enable();else document.addEventListener('DOMContentLoaded',enable,{once:true});
      document.addEventListener('DOMContentLoaded',()=>{if(path())return;const observer=new MutationObserver(()=>{if(path())observer.disconnect();});observer.observe(document.documentElement,{childList:true,subtree:true});},{once:true});
    })();
    """;
}
