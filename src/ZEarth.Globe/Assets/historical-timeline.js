/* Embedded Wayback UI. Dates refer to archive release, not photo acquisition. */
(function (root) {
'use strict';
function trustedTemplate(value) {
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && /^wayback(?:-[a-z0-9]+)?\.maptiles\.arcgis\.com$/i.test(url.hostname) &&
      url.pathname.includes('/World_Imagery/') && url.pathname.includes('/tile/') &&
      ['{z}','{y}','{x}'].every(k => value.includes(k));
  } catch (_) {return false;}
}
function normalize(input) {
  if (!Array.isArray(input)) return [];
  const unique = new Set();
  return input.filter(r => {
    if (!r || !/^\d{1,8}$/.test(String(r.releaseId || '')) ||
        !/^\d{4}-\d{2}-\d{2}$/.test(String(r.date || '')) ||
        !Number.isFinite(Date.parse(r.date+'T00:00:00Z')) ||
        !trustedTemplate(r.tileUrl) || unique.has(String(r.releaseId))) return false;
    unique.add(String(r.releaseId)); return true;
  }).map(r => ({releaseId:String(r.releaseId),date:r.date,tileUrl:r.tileUrl}))
    .sort((a,b) => a.date.localeCompare(b.date) || a.releaseId.localeCompare(b.releaseId));
}
function closestIndex(a,date) {
  if(!a.length) return -1;
  let result=0;
  for(let i=0;i<a.length;i++){if(a[i].date <= date)result=i;else break;}
  return result;
}
function create(opts){
  const host=opts.element;
  if(!host)throw new Error('Missing historical timeline');
  const q=id=>host.querySelector('#'+id);
  const slider=q('history-range'),year=q('history-year'),date=q('history-date'),status=q('history-status');
  let records=[],index=-1,timer=null;
  const message=value=>{status.textContent=value;};
  const stop=()=>{if(timer!==null)clearTimeout(timer);timer=null;};
  const render=()=>{
    const entry=records[index];
    slider.disabled=!entry;
    slider.max=String(Math.max(0,records.length-1));
    slider.value=String(Math.max(index,0));
    ['history-oldest','history-prev'].forEach(k=>q(k).disabled=index<=0);
    ['history-next','history-newest'].forEach(k=>q(k).disabled=index<0||index>=records.length-1);
    date.disabled=!entry;year.disabled=!entry;
    date.min=records[0]?.date||''; date.max=records[records.length-1]?.date||'';
    if(entry)date.value=entry.date;
    year.replaceChildren();
    for(const value of [...new Set(records.map(r=>r.date.slice(0,4)))]){
      const option=document.createElement('option');option.value=value;option.textContent=value;year.appendChild(option);
    }
    if(entry)year.value=entry.date.slice(0,4);
    q('history-selected').textContent=entry ? 'Release '+entry.date : 'No historical release selected';
    q('history-period').textContent=entry ? String(index+1)+' / '+records.length+' releases | '+records[0].date+' - '+records[records.length-1].date : 'No releases';
  };
  const select=(i,apply=true)=>{
    stop();
    if(!records.length){render();return;}
    index=Math.max(0,Math.min(records.length-1,Math.round(Number(i)||0)));
    render();
    if(apply){
      const release=records[index];
      try{
        const result=opts.onSelect(release);
        message(result===false?'This release could not be requested.':
         'Requested archived release '+release.date+'. Tiles may still be loading.');
      }catch(e){message('Imagery request failed: '+String(e.message||e));}
    }
  };
  const setReleases=input=>{
    stop();
    const previous=records[index]?.releaseId;
    records=normalize(input);
    host.hidden=false;
    if(!records.length){index=-1;render();message('No validated Wayback releases returned.');return;}
    const old=records.findIndex(r=>r.releaseId===previous);
    select(old>=0?old:records.length-1);
  };
  const load=()=>{stop();host.hidden=false;message('Loading public Esri Wayback release catalog...');};
  const error=text=>{stop();host.hidden=false;message(text||'Archive unavailable. Retry later.');};
  const close=()=>{stop();host.hidden=true;opts.onClose?.();};
  q('history-close').addEventListener('click',close);
  q('history-prev').addEventListener('click',()=>select(index-1));
  q('history-next').addEventListener('click',()=>select(index+1));
  q('history-oldest').addEventListener('click',()=>select(0));
  q('history-newest').addEventListener('click',()=>select(records.length-1));
  slider.addEventListener('input',()=>{const requested=Number(slider.value);stop();timer=setTimeout(()=>select(requested),200);});
  slider.addEventListener('change',()=>select(Number(slider.value)));
  date.addEventListener('change',()=>{
    if(!date.value||!records.length)return;
    const wanted=date.value;select(closestIndex(records,wanted));
    if(records[index].date!==wanted) message('No archived release on '+wanted+'; nearest on/before: '+records[index].date);
  });
  year.addEventListener('change',()=>{
    const values=records.map((r,i)=>r.date.startsWith(year.value)?i:-1).filter(i=>i>=0);
    if(values.length)select(values[values.length-1]);
  });
  host.addEventListener('keydown',e=>{if(e.key==='Escape'){close();e.stopPropagation();}});
  render();
  return {load,error,close,setReleases,isVisible:()=>!host.hidden};
}
const api=Object.freeze({trustedTemplate,normalize,closestIndex,create});
if(root)root.ZEarthHistoricalTimeline=api;
if(typeof module==='object'&&module.exports)module.exports=api;
})(typeof globalThis!=='undefined'?globalThis:null);
