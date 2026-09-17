import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {RoomEnvironment} from 'three/addons/environments/RoomEnvironment.js';
import {MeshoptDecoder} from './vendor/meshopt_decoder.mjs';
const send = value => window.chrome.webview.postMessage(value);
const label=document.getElementById('message'), hint=document.getElementById('hint'), picker=document.getElementById('pick-image');
const loadingText=document.getElementById('loading-text'),video=document.getElementById('loading-video'),blocks=document.getElementById('loading-blocks'),fallback=document.getElementById('loading-fallback');
const pixels=blocks.getContext('2d',{willReadFrequently:true});
let blocked=false,press=null,modelLoading=false,playing=false,lastVideoTime=-1,videoFailed=false;
let status={busy:false,text:'',persistent:false},localError='';
const requestPhoto=()=>{if(!blocked)send({type:'pick-image'});};
picker.addEventListener('click',requestPhoto);
let renderer, camera, controls, scene, current, sequence=0;
function drawBlocks(){
 if(!playing||video.readyState<2||video.currentTime===lastVideoTime)return;
 lastVideoTime=video.currentTime;
 // Retain the supplied video's moving pixels; remove its pale recording background in the UI.
 pixels.clearRect(0,0,165,125);pixels.drawImage(video,132,12,165,125,0,0,165,125);
 const frame=pixels.getImageData(0,0,165,125),rgba=frame.data;
 for(let i=0;i<rgba.length;i+=4){const tone=Math.max(rgba[i],rgba[i+1],rgba[i+2]);rgba[i+3]=Math.max(0,Math.min(255,(110-tone)*5));rgba[i]=29;rgba[i+1]=29;rgba[i+2]=31;}
 pixels.putImageData(frame,0,0);
}
function present(){
 blocked=status.busy||modelLoading;picker.disabled=blocked;controls&&(controls.enabled=!blocked);
 renderer?.domElement.setAttribute('aria-disabled',String(blocked));document.body.setAttribute('aria-busy',String(blocked));
 const visible=blocked||status.persistent||!!localError;
 const animate=blocked&&!status.persistent&&!localError;
 label.style.display=visible?'flex':'none';
 loadingText.textContent=localError||(modelLoading?'正在载入 PBR 材质…':status.text)||'准备中…';
 picker.style.display=current||blocked?'none':'flex';
 const idleNotice=/^(模型已|图片已就绪|准备中)/.test(status.text)?'':status.text;
 hint.textContent=visible?'':idleNotice||(current?'拖动旋转 · 滚轮缩放 · 点击添加图片':'');
 blocks.style.display=animate&&!videoFailed?'block':'none';fallback.style.display=animate&&videoFailed?'flex':'none';
 if(animate!==playing){
  playing=animate;
  if(playing){video.muted=true;video.play().catch(()=>{videoFailed=true;present();});}
  else{video.pause();video.currentTime=0;lastVideoTime=-1;}
 }
 window.previewPresentation={busy:blocked,loading:animate,visible,text:loadingText.textContent};
}
video.addEventListener('error',()=>{videoFailed=true;present();});
function dispose(root){if(!root)return;const textures=new Set(),materials=new Set(),geometries=new Set();root.traverse(o=>{if(o.geometry)geometries.add(o.geometry);for(const m of(Array.isArray(o.material)?o.material:o.material?[o.material]:[])){materials.add(m);for(const v of Object.values(m))if(v?.isTexture)textures.add(v);}});textures.forEach(t=>{t.source?.data?.close?.();t.dispose();});materials.forEach(m=>m.dispose());geometries.forEach(g=>g.dispose());}
function clear(preserveStatus=false){
 sequence++;if(current){scene.remove(current);dispose(current);current=null;}
 modelLoading=false;localError='';if(!status.busy&&!preserveStatus)status={busy:false,text:'',persistent:false};
 window.previewStats=null;renderer.domElement.tabIndex=-1;press=null;present();
}
try {
 renderer=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});renderer.setClearColor('#f7f7f9',1);renderer.setSize(Math.max(1,innerWidth),Math.max(1,innerHeight));renderer.clear();renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;document.body.prepend(renderer.domElement);
 const canvas=renderer.domElement;canvas.id='scene';canvas.tabIndex=-1;canvas.setAttribute('role','button');canvas.setAttribute('aria-label','3D 预览，点击或按 Enter 添加图片，拖动旋转，滚轮缩放');
 canvas.addEventListener('pointerdown',e=>{press=current&&e.button===0?{id:e.pointerId,x:e.clientX,y:e.clientY,time:performance.now(),moved:false}:null;});
 canvas.addEventListener('pointermove',e=>{if(press&&e.pointerId===press.id&&Math.hypot(e.clientX-press.x,e.clientY-press.y)>5)press.moved=true;});
 canvas.addEventListener('pointercancel',()=>press=null);
 canvas.addEventListener('pointerup',e=>{const p=press;press=null;if(p&&e.pointerId===p.id&&e.button===0&&!p.moved&&Math.hypot(e.clientX-p.x,e.clientY-p.y)<=5&&performance.now()-p.time<400)requestPhoto();});
 canvas.addEventListener('keydown',e=>{if(current&&(e.key==='Enter'||e.key===' ')){e.preventDefault();requestPhoto();}});
 scene=new THREE.Scene();scene.background=new THREE.Color('#f7f7f9');
 const room=new RoomEnvironment(),pmrem=new THREE.PMREMGenerator(renderer);scene.environment=pmrem.fromScene(room,0.04).texture;room.dispose();pmrem.dispose();scene.environmentIntensity=1;
 camera=new THREE.PerspectiveCamera(38,1,0.01,100);camera.up.set(0,0,1);
 const sun=new THREE.DirectionalLight(0xffffff,2.5);sun.position.set(3,-4,6);scene.add(sun);scene.add(new THREE.HemisphereLight(0xffffff,0xc2c5cc,1));
 controls=new OrbitControls(camera,renderer.domElement);controls.enablePan=false;controls.enableDamping=true;controls.mouseButtons={LEFT:THREE.MOUSE.ROTATE,MIDDLE:THREE.MOUSE.DOLLY,RIGHT:THREE.MOUSE.ROTATE};controls.minDistance=.15;controls.maxDistance=15;
 new ResizeObserver(()=>{renderer.setSize(innerWidth,innerHeight);camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();}).observe(document.body);
 renderer.setAnimationLoop(()=>{drawBlocks();if(!playing){controls.update();renderer.render(scene,camera);}});
 const manager=new THREE.LoadingManager();manager.setURLModifier(url=>{if(/^(blob:|data:|https:\/\/copilot\.local\/asset\.glb)/.test(url))return url;throw new Error('只支持模型内嵌资源');});
 let textureErrors=[];manager.onError=url=>textureErrors.push(url);
 const loader=new GLTFLoader(manager).setMeshoptDecoder(MeshoptDecoder);
 window.chrome.webview.addEventListener('message',async e=>{
  const m=e.data;
  if(m.type==='presentation'){status={busy:!!m.busy,text:m.text||'',persistent:!!m.persistent};localError='';present();return;}
  if(m.type==='clear'){clear();return;}if(m.type!=='load')return;
  clear(true);modelLoading=true;textureErrors=[];const revision=sequence;present();
  try{
   const gltf=await loader.loadAsync(m.url);if(revision!==sequence){dispose(gltf.scene);return;}
   if(textureErrors.length){dispose(gltf.scene);throw new Error('有贴图未能载入，请重新载入模型');}
   current=gltf.scene;current.rotation.x=Math.PI/2;current.updateMatrixWorld(true);
   let box=new THREE.Box3().setFromObject(current),center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3()),extent=Math.max(size.x,size.y,size.z);if(!Number.isFinite(extent)||extent<=0)throw new Error('模型没有有效尺寸');
   const holder=new THREE.Group();holder.add(current);current.position.sub(center);holder.scale.setScalar(1/extent);current=holder;scene.add(current);
   controls.target.set(0,0,0);camera.position.set(1.45,-2,1.15);controls.update();
   const parts=[];current.traverse(o=>{if(o.isMesh)for(const m of(Array.isArray(o.material)?o.material:[o.material]))parts.push({material:m.type,base:!!m.map,metallic:!!m.metalnessMap,roughness:!!m.roughnessMap,normal:!!m.normalMap,offset:m.map?.offset.toArray(),repeat:m.map?.repeat.toArray(),wrap:m.map?[m.map.wrapS,m.map.wrapT]:null});});
   window.previewStats={parts,triangles:0,pbr:parts.every(p=>p.material==='MeshStandardMaterial'||p.material==='MeshPhysicalMaterial')};current.traverse(o=>{if(o.isMesh)window.previewStats.triangles+=(o.geometry.index?.count??o.geometry.attributes.position.count)/3;});
   renderer.render(scene,camera);modelLoading=false;canvas.tabIndex=0;present();send({type:'loaded',id:m.id,stats:window.previewStats});
  }catch(error){if(revision!==sequence)return;modelLoading=false;localError='预览载入失败，请重新载入模型';present();send({type:'error',id:m.id,message:String(error.message)});}
 });
 present();renderer.render(scene,camera);window.previewFirstFrameReady=true;send({type:'ready'});
}catch(error){modelLoading=false;localError='无法启动材质预览';present();send({type:'error',message:String(error.message)});}
