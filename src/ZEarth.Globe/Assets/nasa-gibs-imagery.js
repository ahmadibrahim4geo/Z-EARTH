// Public NASA GIBS dated imagery overlay; not Google Earth historical imagery.
(function(root){
  'use strict';
  const catalog = Object.freeze({
    MODIS_Terra_CorrectedReflectance_TrueColor: { name:'Terra MODIS true color', start:'2000-02-24' },
    MODIS_Aqua_CorrectedReflectance_TrueColor: { name:'Aqua MODIS true color', start:'2002-07-03' }
  });
  const url = 'https://gibs.earthdata.nasa.gov/wms/epsg3857/best/wms.cgi';
  function create({Cesium,viewer,onStatus=()=>{}}){
    if(!Cesium?.WebMapServiceImageryProvider || !viewer?.imageryLayers) throw new Error('Cesium WMS imagery support is required');
    let current = null, selection = null;
    function set(layer,date){
      if (!Object.prototype.hasOwnProperty.call(catalog,layer)) {
        return {success:false,error:'The NASA GIBS layer is not in the approved public catalog.'};
      }
      if(typeof date!=='string'|| !/^\d{4}-\d{2}-\d{2}$/.test(date) ||
          !Number.isFinite(Date.parse(date+'T00:00:00Z')) ||
          new Date(date+'T00:00:00Z').toISOString().slice(0,10)!==date ||
          date<catalog[layer].start || date>new Date().toISOString().slice(0,10)){
        return {success:false,error:'Choose a valid date within the available mission interval.'};
      }
      try {
        const provider=new Cesium.WebMapServiceImageryProvider({
          url,layers:layer,
          parameters:{version:'1.1.1',format:'image/png',transparent:true,time:date},
          credit:'NASA GIBS / NASA Earthdata — daily MODIS visualization; imagery availability varies by date and location',
          enablePickFeatures:false,
          maximumLevel:12
        });
        const next=viewer.imageryLayers.addImageryProvider(provider);
        if(current) viewer.imageryLayers.remove(current,true);
        current=next; selection={layer,date};
        viewer.scene?.requestRender?.();
        onStatus({event:'historical.nasaConfigured',layer,date,configured:true,tilesVerified:false});
        if(provider.errorEvent?.addEventListener){
          provider.errorEvent.addEventListener(e=>onStatus({
            event:'historical.nasaTileError',layer,date,message:String(e?.message||'GIBS tile unavailable')
          }));
        }
        return {success:true,configured:true,tilesVerified:false,layer,date,attribution:'NASA GIBS'};
      }catch(error){
        onStatus({event:'historical.nasaTileError',layer,date,message:String(error?.message||error)});
        return {success:false,error:String(error?.message||error)};
      }
    }
    function clear(){
      if(current) viewer.imageryLayers.remove(current,true);
      current=null;selection=null;viewer.scene?.requestRender?.();
      onStatus({event:'historical.nasaConfigured',configured:false});
      return {success:true,configured:false};
    }
    return {set,clear,getState:()=>selection?{...selection,configured:true}:{configured:false}};
  }
  root.ZEarthNasaGibs=Object.freeze({create,catalog});
})(typeof window!=='undefined'?window:globalThis);
