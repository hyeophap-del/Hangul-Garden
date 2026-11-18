import {mkdirSync,writeFileSync,copyFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const dir=new URL('../dist/',import.meta.url);mkdirSync(dir,{recursive:true});
function json(file,data){writeFileSync(new URL(file,dir),JSON.stringify(data,null,2)+'\n');}
const types=['oculus_touch','knuckles','vive_controller','holographic_controller'];
json('actions.json',{
  action_sets:[{name:'/actions/keyboard',usage:'leftright'}],
  actions:[{name:'/actions/keyboard/in/distance',type:'vector2'},{name:'/actions/keyboard/in/click',type:'boolean'},{name:'/actions/keyboard/in/grip',type:'boolean'},{name:'/actions/keyboard/in/pull',type:'vector1'},{name:'/actions/keyboard/in/menu',type:'boolean'},{name:'/actions/keyboard/in/pose',type:'pose'},{name:'/actions/keyboard/out/haptic',type:'vibration'}],
  default_bindings:types.map(controller_type=>({controller_type,binding_url:`bindings_${controller_type}.json`})),
  localization:[{language_tag:'en_US','/actions/keyboard':'Hangeul VR Keyboard','/actions/keyboard/in/click':'Select key (trigger)','/actions/keyboard/in/grip':'Toggle modifier (left grip)','/actions/keyboard/in/pull':'Trigger analog pressure','/actions/keyboard/in/menu':'Alternative toggle (left B/menu double tap)','/actions/keyboard/in/pose':'Pointer pose','/actions/keyboard/out/haptic':'Key feedback'}]
});
for(const type of types){
  const sources=[],poses=[],haptics=[];
  for(const hand of ['left','right']){
    const base=`/user/hand/${hand}`;
    sources.push({path:`${base}/input/trigger`,mode:'button',parameters:{click_activate_threshold:'0.35',click_deactivate_threshold:'0.15'},inputs:{click:{output:'/actions/keyboard/in/click'}}});
    sources.push({path:`${base}/input/trigger`,mode:'trigger',inputs:{pull:{output:'/actions/keyboard/in/pull'}}});
    if(hand==='left')sources.push({path:`${base}/input/${type==='oculus_touch'||type==='knuckles'?'b':'application_menu'}`,mode:'button',inputs:{click:{output:'/actions/keyboard/in/menu'}}});
    sources.push({path:`${base}/input/grip`,mode:'button',parameters:type==='knuckles'?{force_input:'force',click_activate_threshold:'0.65',click_deactivate_threshold:'0.4'}:{click_activate_threshold:'0.75',click_deactivate_threshold:'0.5'},inputs:{click:{output:'/actions/keyboard/in/grip'}}});
    sources.push({path:`${base}/input/${type==='vive_controller'?'trackpad':'joystick'}`,mode:type==='vive_controller'?'trackpad':'joystick',inputs:{position:{output:'/actions/keyboard/in/distance'}}});
    poses.push({path:`${base}/pose/tip`,output:'/actions/keyboard/in/pose'});
    haptics.push({path:`${base}/output/haptic`,output:'/actions/keyboard/out/haptic'});
  }
  json(`bindings_${type}.json`,{controller_type:type,name:'Hangeul VR defaults',description:'Point and trigger. Hold left grip and double tap right trigger to toggle. Alternative hotkeys selectable in app.',bindings:{'/actions/keyboard':{sources,poses,haptics}}});
}
copyFileSync(new URL('../vendor/OpenVR-LICENSE.txt',import.meta.url),new URL('OpenVR-LICENSE.txt',dir));
copyFileSync(new URL('../README.md',import.meta.url),new URL('README.md',dir));
console.log('SteamVR action bindings generated in '+fileURLToPath(dir));
