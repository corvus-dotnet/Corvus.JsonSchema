//! Licensed to the .NET Foundation under one or more agreements.
//! The .NET Foundation licenses this file to you under the MIT license.

var e=!1;const t=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,4,1,96,0,0,3,2,1,0,10,8,1,6,0,6,64,25,11,11])),o=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,15,1,13,0,65,1,253,15,65,2,253,15,253,128,2,11])),n=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,10,1,8,0,65,0,253,15,253,98,11])),r=Symbol.for("wasm promise_control");function i(e,t){let o=null;const n=new Promise((function(n,r){o={isDone:!1,promise:null,resolve:t=>{o.isDone||(o.isDone=!0,n(t),e&&e())},reject:e=>{o.isDone||(o.isDone=!0,r(e),t&&t())}}}));o.promise=n;const i=n;return i[r]=o,{promise:i,promise_control:o}}function s(e){return e[r]}function a(e){e&&function(e){return void 0!==e[r]}(e)||Be(!1,"Promise is not controllable")}const l="__mono_message__",c=["debug","log","trace","warn","info","error"],d="MONO_WASM: ";let u,f,m,g,p,h;function w(e){g=e}function b(e){if(Pe.diagnosticTracing){const t="function"==typeof e?e():e;console.debug(d+t)}}function y(e,...t){console.info(d+e,...t)}function v(e,...t){console.info(e,...t)}function E(e,...t){console.warn(d+e,...t)}function _(e,...t){if(t&&t.length>0&&t[0]&&"object"==typeof t[0]){if(t[0].silent)return;if(t[0].toString)return void console.error(d+e,t[0].toString())}console.error(d+e,...t)}function x(e,t,o){return function(...n){try{let r=n[0];if(void 0===r)r="undefined";else if(null===r)r="null";else if("function"==typeof r)r=r.toString();else if("string"!=typeof r)try{r=JSON.stringify(r)}catch(e){r=r.toString()}t(o?JSON.stringify({method:e,payload:r,arguments:n.slice(1)}):[e+r,...n.slice(1)])}catch(e){m.error(`proxyConsole failed: ${e}`)}}}function j(e,t,o){f=t,g=e,m={...t};const n=`${o}/console`.replace("https://","wss://").replace("http://","ws://");u=new WebSocket(n),u.addEventListener("error",A),u.addEventListener("close",S),function(){for(const e of c)f[e]=x(`console.${e}`,T,!0)}()}function R(e){let t=30;const o=()=>{u?0==u.bufferedAmount||0==t?(e&&v(e),function(){for(const e of c)f[e]=x(`console.${e}`,m.log,!1)}(),u.removeEventListener("error",A),u.removeEventListener("close",S),u.close(1e3,e),u=void 0):(t--,globalThis.setTimeout(o,100)):e&&m&&m.log(e)};o()}function T(e){u&&u.readyState===WebSocket.OPEN?u.send(e):m.log(e)}function A(e){m.error(`[${g}] proxy console websocket error: ${e}`,e)}function S(e){m.debug(`[${g}] proxy console websocket closed: ${e}`,e)}function D(){Pe.preferredIcuAsset=O(Pe.config);let e="invariant"==Pe.config.globalizationMode;if(!e)if(Pe.preferredIcuAsset)Pe.diagnosticTracing&&b("ICU data archive(s) available, disabling invariant mode");else{if("custom"===Pe.config.globalizationMode||"all"===Pe.config.globalizationMode||"sharded"===Pe.config.globalizationMode){const e="invariant globalization mode is inactive and no ICU data archives are available";throw _(`ERROR: ${e}`),new Error(e)}Pe.diagnosticTracing&&b("ICU data archive(s) not available, using invariant globalization mode"),e=!0,Pe.preferredIcuAsset=null}const t="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",o=Pe.config.environmentVariables;if(void 0===o[t]&&e&&(o[t]="1"),void 0===o.TZ)try{const e=Intl.DateTimeFormat().resolvedOptions().timeZone||null;e&&(o.TZ=e)}catch(e){y("failed to detect timezone, will fallback to UTC")}}function O(e){var t;if((null===(t=e.resources)||void 0===t?void 0:t.icu)&&"invariant"!=e.globalizationMode){const t=e.applicationCulture||(ke?globalThis.navigator&&globalThis.navigator.languages&&globalThis.navigator.languages[0]:Intl.DateTimeFormat().resolvedOptions().locale),o=e.resources.icu;let n=null;if("custom"===e.globalizationMode){if(o.length>=1)return o[0].name}else t&&"all"!==e.globalizationMode?"sharded"===e.globalizationMode&&(n=function(e){const t=e.split("-")[0];return"en"===t||["fr","fr-FR","it","it-IT","de","de-DE","es","es-ES"].includes(e)?"icudt_EFIGS.dat":["zh","ko","ja"].includes(t)?"icudt_CJK.dat":"icudt_no_CJK.dat"}(t)):n="icudt.dat";if(n)for(let e=0;e<o.length;e++){const t=o[e];if(t.virtualPath===n)return t.name}}return e.globalizationMode="invariant",null}(new Date).valueOf();const C=class{constructor(e){this.url=e}toString(){return this.url}};async function k(e,t){try{const o="function"==typeof globalThis.fetch;if(Se){const n=e.startsWith("file://");if(!n&&o)return globalThis.fetch(e,t||{credentials:"same-origin"});p||(h=Ne.require("url"),p=Ne.require("fs")),n&&(e=h.fileURLToPath(e));const r=await p.promises.readFile(e);return{ok:!0,headers:{length:0,get:()=>null},url:e,arrayBuffer:()=>r,json:()=>JSON.parse(r),text:()=>{throw new Error("NotImplementedException")}}}if(o)return globalThis.fetch(e,t||{credentials:"same-origin"});if("function"==typeof read)return{ok:!0,url:e,headers:{length:0,get:()=>null},arrayBuffer:()=>new Uint8Array(read(e,"binary")),json:()=>JSON.parse(read(e,"utf8")),text:()=>read(e,"utf8")}}catch(t){return{ok:!1,url:e,status:500,headers:{length:0,get:()=>null},statusText:"ERR28: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t},text:()=>{throw t}}}throw new Error("No fetch implementation available")}function I(e){return"string"!=typeof e&&Be(!1,"url must be a string"),!M(e)&&0!==e.indexOf("./")&&0!==e.indexOf("../")&&globalThis.URL&&globalThis.document&&globalThis.document.baseURI&&(e=new URL(e,globalThis.document.baseURI).toString()),e}const U=/^[a-zA-Z][a-zA-Z\d+\-.]*?:\/\//,P=/[a-zA-Z]:[\\/]/;function M(e){return Se||Ie?e.startsWith("/")||e.startsWith("\\")||-1!==e.indexOf("///")||P.test(e):U.test(e)}let L,N=0;const $=[],z=[],W=new Map,F={"js-module-threads":!0,"js-module-runtime":!0,"js-module-dotnet":!0,"js-module-native":!0,"js-module-diagnostics":!0},B={...F,"js-module-library-initializer":!0},V={...F,dotnetwasm:!0,heap:!0,manifest:!0},q={...B,manifest:!0},H={...B,dotnetwasm:!0},J={dotnetwasm:!0,symbols:!0},Z={...B,dotnetwasm:!0,symbols:!0},Q={symbols:!0};function G(e){return!("icu"==e.behavior&&e.name!=Pe.preferredIcuAsset)}function K(e,t,o){null!=t||(t=[]),Be(1==t.length,`Expect to have one ${o} asset in resources`);const n=t[0];return n.behavior=o,X(n),e.push(n),n}function X(e){V[e.behavior]&&W.set(e.behavior,e)}function Y(e){Be(V[e],`Unknown single asset behavior ${e}`);const t=W.get(e);if(t&&!t.resolvedUrl)if(t.resolvedUrl=Pe.locateFile(t.name),F[t.behavior]){const e=ge(t);e?("string"!=typeof e&&Be(!1,"loadBootResource response for 'dotnetjs' type should be a URL string"),t.resolvedUrl=e):t.resolvedUrl=ce(t.resolvedUrl,t.behavior)}else if("dotnetwasm"!==t.behavior)throw new Error(`Unknown single asset behavior ${e}`);return t}function ee(e){const t=Y(e);return Be(t,`Single asset for ${e} not found`),t}let te=!1;async function oe(){if(!te){te=!0,Pe.diagnosticTracing&&b("mono_download_assets");try{const e=[],t=[],o=(e,t)=>{!Z[e.behavior]&&G(e)&&Pe.expected_instantiated_assets_count++,!H[e.behavior]&&G(e)&&(Pe.expected_downloaded_assets_count++,t.push(se(e)))};for(const t of $)o(t,e);for(const e of z)o(e,t);Pe.allDownloadsQueued.promise_control.resolve(),Promise.all([...e,...t]).then((()=>{Pe.allDownloadsFinished.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),await Pe.runtimeModuleLoaded.promise;const n=async e=>{const t=await e;if(t.buffer){if(!Z[t.behavior]){t.buffer&&"object"==typeof t.buffer||Be(!1,"asset buffer must be array-like or buffer-like or promise of these"),"string"!=typeof t.resolvedUrl&&Be(!1,"resolvedUrl must be string");const e=t.resolvedUrl,o=await t.buffer,n=new Uint8Array(o);pe(t),await Ue.beforeOnRuntimeInitialized.promise,Ue.instantiate_asset(t,e,n)}}else J[t.behavior]?("symbols"===t.behavior&&(await Ue.instantiate_symbols_asset(t),pe(t)),J[t.behavior]&&++Pe.actual_downloaded_assets_count):(t.isOptional||Be(!1,"Expected asset to have the downloaded buffer"),!H[t.behavior]&&G(t)&&Pe.expected_downloaded_assets_count--,!Z[t.behavior]&&G(t)&&Pe.expected_instantiated_assets_count--)},r=[],i=[];for(const t of e)r.push(n(t));for(const e of t)i.push(n(e));Promise.all(r).then((()=>{Ce||Ue.coreAssetsInMemory.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),Promise.all(i).then((async()=>{Ce||(await Ue.coreAssetsInMemory.promise,Ue.allAssetsInMemory.promise_control.resolve())})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e}))}catch(e){throw Pe.err("Error in mono_download_assets: "+e),e}}}let ne=!1;function re(){if(ne)return;ne=!0;const e=Pe.config,t=[];if(e.assets)for(const t of e.assets)"object"!=typeof t&&Be(!1,`asset must be object, it was ${typeof t} : ${t}`),"string"!=typeof t.behavior&&Be(!1,"asset behavior must be known string"),"string"!=typeof t.name&&Be(!1,"asset name must be string"),t.resolvedUrl&&"string"!=typeof t.resolvedUrl&&Be(!1,"asset resolvedUrl could be string"),t.hash&&"string"!=typeof t.hash&&Be(!1,"asset resolvedUrl could be string"),t.pendingDownload&&"object"!=typeof t.pendingDownload&&Be(!1,"asset pendingDownload could be object"),t.isCore?$.push(t):z.push(t),X(t);else if(e.resources){const o=e.resources;o.wasmNative||Be(!1,"resources.wasmNative must be defined"),o.jsModuleNative||Be(!1,"resources.jsModuleNative must be defined"),o.jsModuleRuntime||Be(!1,"resources.jsModuleRuntime must be defined"),K(z,o.wasmNative,"dotnetwasm"),K(t,o.jsModuleNative,"js-module-native"),K(t,o.jsModuleRuntime,"js-module-runtime"),o.jsModuleDiagnostics&&K(t,o.jsModuleDiagnostics,"js-module-diagnostics");const n=(e,t,o)=>{const n=e;n.behavior=t,o?(n.isCore=!0,$.push(n)):z.push(n)};if(o.coreAssembly)for(let e=0;e<o.coreAssembly.length;e++)n(o.coreAssembly[e],"assembly",!0);if(o.assembly)for(let e=0;e<o.assembly.length;e++)n(o.assembly[e],"assembly",!o.coreAssembly);if(0!=e.debugLevel&&Pe.isDebuggingSupported()){if(o.corePdb)for(let e=0;e<o.corePdb.length;e++)n(o.corePdb[e],"pdb",!0);if(o.pdb)for(let e=0;e<o.pdb.length;e++)n(o.pdb[e],"pdb",!o.corePdb)}if(e.loadAllSatelliteResources&&o.satelliteResources)for(const e in o.satelliteResources)for(let t=0;t<o.satelliteResources[e].length;t++){const r=o.satelliteResources[e][t];r.culture=e,n(r,"resource",!o.coreAssembly)}if(o.coreVfs)for(let e=0;e<o.coreVfs.length;e++)n(o.coreVfs[e],"vfs",!0);if(o.vfs)for(let e=0;e<o.vfs.length;e++)n(o.vfs[e],"vfs",!o.coreVfs);const r=O(e);if(r&&o.icu)for(let e=0;e<o.icu.length;e++){const t=o.icu[e];t.name===r&&n(t,"icu",!1)}if(o.wasmSymbols)for(let e=0;e<o.wasmSymbols.length;e++)n(o.wasmSymbols[e],"symbols",!1)}if(e.appsettings)for(let t=0;t<e.appsettings.length;t++){const o=e.appsettings[t],n=he(o);"appsettings.json"!==n&&n!==`appsettings.${e.applicationEnvironment}.json`||z.push({name:o,behavior:"vfs",cache:"no-cache",useCredentials:!0})}e.assets=[...$,...z,...t]}async function ie(e){const t=await se(e);return await t.pendingDownloadInternal.response,t.buffer}async function se(e){try{return await ae(e)}catch(t){if(!Pe.enableDownloadRetry)throw t;if(Ie||Se)throw t;if(e.pendingDownload&&e.pendingDownloadInternal==e.pendingDownload)throw t;if(e.resolvedUrl&&-1!=e.resolvedUrl.indexOf("file://"))throw t;if(t&&404==t.status)throw t;e.pendingDownloadInternal=void 0,await Pe.allDownloadsQueued.promise;try{return Pe.diagnosticTracing&&b(`Retrying download '${e.name}'`),await ae(e)}catch(t){return e.pendingDownloadInternal=void 0,await new Promise((e=>globalThis.setTimeout(e,100))),Pe.diagnosticTracing&&b(`Retrying download (2) '${e.name}' after delay`),await ae(e)}}}async function ae(e){for(;L;)await L.promise;try{++N,N==Pe.maxParallelDownloads&&(Pe.diagnosticTracing&&b("Throttling further parallel downloads"),L=i());const t=await async function(e){if(e.pendingDownload&&(e.pendingDownloadInternal=e.pendingDownload),e.pendingDownloadInternal&&e.pendingDownloadInternal.response)return e.pendingDownloadInternal.response;if(e.buffer){const t=await e.buffer;return e.resolvedUrl||(e.resolvedUrl="undefined://"+e.name),e.pendingDownloadInternal={url:e.resolvedUrl,name:e.name,response:Promise.resolve({ok:!0,arrayBuffer:()=>t,json:()=>JSON.parse(new TextDecoder("utf-8").decode(t)),text:()=>{throw new Error("NotImplementedException")},headers:{get:()=>{}}})},e.pendingDownloadInternal.response}const t=e.loadRemote&&Pe.config.remoteSources?Pe.config.remoteSources:[""];let o;for(let n of t){n=n.trim(),"./"===n&&(n="");const t=le(e,n);e.name===t?Pe.diagnosticTracing&&b(`Attempting to download '${t}'`):Pe.diagnosticTracing&&b(`Attempting to download '${t}' for ${e.name}`);try{e.resolvedUrl=t;const n=fe(e);if(e.pendingDownloadInternal=n,o=await n.response,!o||!o.ok)continue;return o}catch(e){o||(o={ok:!1,url:t,status:0,statusText:""+e});continue}}const n=e.isOptional||e.name.match(/\.pdb$/)&&Pe.config.ignorePdbLoadErrors;if(o||Be(!1,`Response undefined ${e.name}`),!n){const t=new Error(`download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`);throw t.status=o.status,t}y(`optional download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`)}(e);return t?(J[e.behavior]||(e.buffer=await t.arrayBuffer(),++Pe.actual_downloaded_assets_count),e):e}finally{if(--N,L&&N==Pe.maxParallelDownloads-1){Pe.diagnosticTracing&&b("Resuming more parallel downloads");const e=L;L=void 0,e.promise_control.resolve()}}}function le(e,t){let o;return null==t&&Be(!1,`sourcePrefix must be provided for ${e.name}`),e.resolvedUrl?o=e.resolvedUrl:(o=""===t?"assembly"===e.behavior||"pdb"===e.behavior?e.name:"resource"===e.behavior&&e.culture&&""!==e.culture?`${e.culture}/${e.name}`:e.name:t+e.name,o=ce(Pe.locateFile(o),e.behavior)),o&&"string"==typeof o||Be(!1,"attemptUrl need to be path or url string"),o}function ce(e,t){return Pe.modulesUniqueQuery&&q[t]&&(e+=Pe.modulesUniqueQuery),e}let de=0;const ue=new Set;function fe(e){try{e.resolvedUrl||Be(!1,"Request's resolvedUrl must be set");const t=function(e){let t=e.resolvedUrl;if(Pe.loadBootResource){const o=ge(e);if(o instanceof Promise)return o;"string"==typeof o&&(t=o)}const o={};return e.cache?o.cache=e.cache:Pe.config.disableNoCacheFetch||(o.cache="no-cache"),e.useCredentials?o.credentials="include":!Pe.config.disableIntegrityCheck&&e.hash&&(o.integrity=e.hash),Pe.fetch_like(t,o)}(e),o={name:e.name,url:e.resolvedUrl,response:t};return ue.add(e.name),o.response.then((()=>{"assembly"==e.behavior&&Pe.loadedAssemblies.push(e.name),de++,Pe.onDownloadResourceProgress&&Pe.onDownloadResourceProgress(de,ue.size)})),o}catch(t){const o={ok:!1,url:e.resolvedUrl,status:500,statusText:"ERR29: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t}};return{name:e.name,url:e.resolvedUrl,response:Promise.resolve(o)}}}const me={resource:"assembly",assembly:"assembly",pdb:"pdb",icu:"globalization",vfs:"configuration",manifest:"manifest",dotnetwasm:"dotnetwasm","js-module-dotnet":"dotnetjs","js-module-native":"dotnetjs","js-module-runtime":"dotnetjs","js-module-threads":"dotnetjs"};function ge(e){var t;if(Pe.loadBootResource){const o=null!==(t=e.hash)&&void 0!==t?t:"",n=e.resolvedUrl,r=me[e.behavior];if(r){const t=Pe.loadBootResource(r,e.name,n,o,e.behavior);return"string"==typeof t?I(t):t}}}function pe(e){e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null}function he(e){let t=e.lastIndexOf("/");return t>=0&&t++,e.substring(t)}async function we(e){e&&await Promise.all((null!=e?e:[]).map((e=>async function(e){try{const t=e.name;if(!e.moduleExports){const o=ce(Pe.locateFile(t),"js-module-library-initializer");Pe.diagnosticTracing&&b(`Attempting to import '${o}' for ${e}`),e.moduleExports=await import(/*! webpackIgnore: true */o)}Pe.libraryInitializers.push({scriptName:t,exports:e.moduleExports})}catch(t){E(`Failed to import library initializer '${e}': ${t}`)}}(e))))}async function be(e,t){if(!Pe.libraryInitializers)return;const o=[];for(let n=0;n<Pe.libraryInitializers.length;n++){const r=Pe.libraryInitializers[n];r.exports[e]&&o.push(ye(r.scriptName,e,(()=>r.exports[e](...t))))}await Promise.all(o)}async function ye(e,t,o){try{await o()}catch(o){throw E(`Failed to invoke '${t}' on library initializer '${e}': ${o}`),Xe(1,o),o}}function ve(e,t){if(e===t)return e;const o={...t};return void 0!==o.assets&&o.assets!==e.assets&&(o.assets=[...e.assets||[],...o.assets||[]]),void 0!==o.resources&&(o.resources=_e(e.resources||{assembly:[],jsModuleNative:[],jsModuleRuntime:[],wasmNative:[]},o.resources)),void 0!==o.environmentVariables&&(o.environmentVariables={...e.environmentVariables||{},...o.environmentVariables||{}}),void 0!==o.runtimeOptions&&o.runtimeOptions!==e.runtimeOptions&&(o.runtimeOptions=[...e.runtimeOptions||[],...o.runtimeOptions||[]]),Object.assign(e,o)}function Ee(e,t){if(e===t)return e;const o={...t};return o.config&&(e.config||(e.config={}),o.config=ve(e.config,o.config)),Object.assign(e,o)}function _e(e,t){if(e===t)return e;const o={...t};return void 0!==o.coreAssembly&&(o.coreAssembly=[...e.coreAssembly||[],...o.coreAssembly||[]]),void 0!==o.assembly&&(o.assembly=[...e.assembly||[],...o.assembly||[]]),void 0!==o.lazyAssembly&&(o.lazyAssembly=[...e.lazyAssembly||[],...o.lazyAssembly||[]]),void 0!==o.corePdb&&(o.corePdb=[...e.corePdb||[],...o.corePdb||[]]),void 0!==o.pdb&&(o.pdb=[...e.pdb||[],...o.pdb||[]]),void 0!==o.jsModuleWorker&&(o.jsModuleWorker=[...e.jsModuleWorker||[],...o.jsModuleWorker||[]]),void 0!==o.jsModuleNative&&(o.jsModuleNative=[...e.jsModuleNative||[],...o.jsModuleNative||[]]),void 0!==o.jsModuleDiagnostics&&(o.jsModuleDiagnostics=[...e.jsModuleDiagnostics||[],...o.jsModuleDiagnostics||[]]),void 0!==o.jsModuleRuntime&&(o.jsModuleRuntime=[...e.jsModuleRuntime||[],...o.jsModuleRuntime||[]]),void 0!==o.wasmSymbols&&(o.wasmSymbols=[...e.wasmSymbols||[],...o.wasmSymbols||[]]),void 0!==o.wasmNative&&(o.wasmNative=[...e.wasmNative||[],...o.wasmNative||[]]),void 0!==o.icu&&(o.icu=[...e.icu||[],...o.icu||[]]),void 0!==o.satelliteResources&&(o.satelliteResources=function(e,t){if(e===t)return e;for(const o in t)e[o]=[...e[o]||[],...t[o]||[]];return e}(e.satelliteResources||{},o.satelliteResources||{})),void 0!==o.modulesAfterConfigLoaded&&(o.modulesAfterConfigLoaded=[...e.modulesAfterConfigLoaded||[],...o.modulesAfterConfigLoaded||[]]),void 0!==o.modulesAfterRuntimeReady&&(o.modulesAfterRuntimeReady=[...e.modulesAfterRuntimeReady||[],...o.modulesAfterRuntimeReady||[]]),void 0!==o.extensions&&(o.extensions={...e.extensions||{},...o.extensions||{}}),void 0!==o.vfs&&(o.vfs=[...e.vfs||[],...o.vfs||[]]),Object.assign(e,o)}function xe(){const e=Pe.config;if(e.environmentVariables=e.environmentVariables||{},e.runtimeOptions=e.runtimeOptions||[],e.resources=e.resources||{assembly:[],jsModuleNative:[],jsModuleWorker:[],jsModuleRuntime:[],wasmNative:[],vfs:[],satelliteResources:{}},e.assets){Pe.diagnosticTracing&&b("config.assets is deprecated, use config.resources instead");for(const t of e.assets){const o={};switch(t.behavior){case"assembly":o.assembly=[t];break;case"pdb":o.pdb=[t];break;case"resource":o.satelliteResources={},o.satelliteResources[t.culture]=[t];break;case"icu":o.icu=[t];break;case"symbols":o.wasmSymbols=[t];break;case"vfs":o.vfs=[t];break;case"dotnetwasm":o.wasmNative=[t];break;case"js-module-threads":o.jsModuleWorker=[t];break;case"js-module-runtime":o.jsModuleRuntime=[t];break;case"js-module-native":o.jsModuleNative=[t];break;case"js-module-diagnostics":o.jsModuleDiagnostics=[t];break;case"js-module-dotnet":break;default:throw new Error(`Unexpected behavior ${t.behavior} of asset ${t.name}`)}_e(e.resources,o)}}e.debugLevel,e.applicationEnvironment||(e.applicationEnvironment="Production"),e.applicationCulture&&(e.environmentVariables.LANG=`${e.applicationCulture}.UTF-8`),Ue.diagnosticTracing=Pe.diagnosticTracing=!!e.diagnosticTracing,Ue.waitForDebugger=e.waitForDebugger,Pe.maxParallelDownloads=e.maxParallelDownloads||Pe.maxParallelDownloads,Pe.enableDownloadRetry=void 0!==e.enableDownloadRetry?e.enableDownloadRetry:Pe.enableDownloadRetry}let je=!1;async function Re(e){var t;if(je)return void await Pe.afterConfigLoaded.promise;let o;try{if(e.configSrc||Pe.config&&0!==Object.keys(Pe.config).length&&(Pe.config.assets||Pe.config.resources)||(e.configSrc="dotnet.boot.js"),o=e.configSrc,je=!0,o&&(Pe.diagnosticTracing&&b("mono_wasm_load_config"),await async function(e){const t=e.configSrc,o=Pe.locateFile(t);let n=null;void 0!==Pe.loadBootResource&&(n=Pe.loadBootResource("manifest",t,o,"","manifest"));let r,i=null;if(n)if("string"==typeof n)n.includes(".json")?(i=await s(I(n)),r=await Ae(i)):r=(await import(I(n))).config;else{const e=await n;"function"==typeof e.json?(i=e,r=await Ae(i)):r=e.config}else o.includes(".json")?(i=await s(ce(o,"manifest")),r=await Ae(i)):r=(await import(ce(o,"manifest"))).config;function s(e){return Pe.fetch_like(e,{method:"GET",credentials:"include",cache:"no-cache"})}Pe.config.applicationEnvironment&&(r.applicationEnvironment=Pe.config.applicationEnvironment),ve(Pe.config,r)}(e)),xe(),await we(null===(t=Pe.config.resources)||void 0===t?void 0:t.modulesAfterConfigLoaded),await be("onRuntimeConfigLoaded",[Pe.config]),e.onConfigLoaded)try{await e.onConfigLoaded(Pe.config,Le),xe()}catch(e){throw _("onConfigLoaded() failed",e),e}xe(),Pe.afterConfigLoaded.promise_control.resolve(Pe.config)}catch(t){const n=`Failed to load config file ${o} ${t} ${null==t?void 0:t.stack}`;throw Pe.config=e.config=Object.assign(Pe.config,{message:n,error:t,isError:!0}),Xe(1,new Error(n)),t}}function Te(){return!!globalThis.navigator&&(Pe.isChromium||Pe.isFirefox)}async function Ae(e){const t=Pe.config,o=await e.json();t.applicationEnvironment||o.applicationEnvironment||(o.applicationEnvironment=e.headers.get("Blazor-Environment")||e.headers.get("DotNet-Environment")||void 0),o.environmentVariables||(o.environmentVariables={});const n=e.headers.get("DOTNET-MODIFIABLE-ASSEMBLIES");n&&(o.environmentVariables.DOTNET_MODIFIABLE_ASSEMBLIES=n);const r=e.headers.get("ASPNETCORE-BROWSER-TOOLS");return r&&(o.environmentVariables.__ASPNETCORE_BROWSER_TOOLS=r),o}"function"!=typeof importScripts||globalThis.onmessage||(globalThis.dotnetSidecar=!0);const Se="object"==typeof process&&"object"==typeof process.versions&&"string"==typeof process.versions.node,De="function"==typeof importScripts,Oe=De&&"undefined"!=typeof dotnetSidecar,Ce=De&&!Oe,ke="object"==typeof window||De&&!Se,Ie=!ke&&!Se;let Ue={},Pe={},Me={},Le={},Ne={},$e=!1;const ze={},We={config:ze},Fe={mono:{},binding:{},internal:Ne,module:We,loaderHelpers:Pe,runtimeHelpers:Ue,diagnosticHelpers:Me,api:Le};function Be(e,t){if(e)return;const o="Assert failed: "+("function"==typeof t?t():t),n=new Error(o);_(o,n),Ue.nativeAbort(n)}function Ve(){return void 0!==Pe.exitCode}function qe(){return Ue.runtimeReady&&!Ve()}function He(){Ve()&&Be(!1,`.NET runtime already exited with ${Pe.exitCode} ${Pe.exitReason}. You can use runtime.runMain() which doesn't exit the runtime.`),Ue.runtimeReady||Be(!1,".NET runtime didn't start yet. Please call dotnet.create() first.")}function Je(){ke&&(globalThis.addEventListener("unhandledrejection",et),globalThis.addEventListener("error",tt))}let Ze,Qe;function Ge(e){Qe&&Qe(e),Xe(e,Pe.exitReason)}function Ke(e){Ze&&Ze(e||Pe.exitReason),Xe(1,e||Pe.exitReason)}function Xe(t,o){var n,r;const i=o&&"object"==typeof o;t=i&&"number"==typeof o.status?o.status:void 0===t?-1:t;const s=i&&"string"==typeof o.message?o.message:""+o;(o=i?o:Ue.ExitStatus?function(e,t){const o=new Ue.ExitStatus(e);return o.message=t,o.toString=()=>t,o}(t,s):new Error("Exit with code "+t+" "+s)).status=t,o.message||(o.message=s);const a=""+(o.stack||(new Error).stack);try{Object.defineProperty(o,"stack",{get:()=>a})}catch(e){}const l=!!o.silent;if(o.silent=!0,Ve())Pe.diagnosticTracing&&b("mono_exit called after exit");else{try{We.onAbort==Ke&&(We.onAbort=Ze),We.onExit==Ge&&(We.onExit=Qe),ke&&(globalThis.removeEventListener("unhandledrejection",et),globalThis.removeEventListener("error",tt)),Ue.runtimeReady?(Ue.jiterpreter_dump_stats&&Ue.jiterpreter_dump_stats(!1),0===t&&(null===(n=Pe.config)||void 0===n?void 0:n.interopCleanupOnExit)&&Ue.forceDisposeProxies(!0,!0),e&&0!==t&&(null===(r=Pe.config)||void 0===r||r.dumpThreadsOnNonZeroExit)):(Pe.diagnosticTracing&&b(`abort_startup, reason: ${o}`),function(e){Pe.allDownloadsQueued.promise_control.reject(e),Pe.allDownloadsFinished.promise_control.reject(e),Pe.afterConfigLoaded.promise_control.reject(e),Pe.wasmCompilePromise.promise_control.reject(e),Pe.runtimeModuleLoaded.promise_control.reject(e),Ue.dotnetReady&&(Ue.dotnetReady.promise_control.reject(e),Ue.afterInstantiateWasm.promise_control.reject(e),Ue.beforePreInit.promise_control.reject(e),Ue.afterPreInit.promise_control.reject(e),Ue.afterPreRun.promise_control.reject(e),Ue.beforeOnRuntimeInitialized.promise_control.reject(e),Ue.afterOnRuntimeInitialized.promise_control.reject(e),Ue.afterPostRun.promise_control.reject(e))}(o))}catch(e){E("mono_exit A failed",e)}try{l||(function(e,t){if(0!==e&&t){const e=Ue.ExitStatus&&t instanceof Ue.ExitStatus?b:_;"string"==typeof t?e(t):(void 0===t.stack&&(t.stack=(new Error).stack+""),t.message?e(Ue.stringify_as_error_with_stack?Ue.stringify_as_error_with_stack(t.message+"\n"+t.stack):t.message+"\n"+t.stack):e(JSON.stringify(t)))}!Ce&&Pe.config&&(Pe.config.logExitCode?Pe.config.forwardConsoleLogsToWS?R("WASM EXIT "+e):v("WASM EXIT "+e):Pe.config.forwardConsoleLogsToWS&&R())}(t,o),function(e){if(ke&&!Ce&&Pe.config&&Pe.config.appendElementOnExit&&document){const t=document.createElement("label");t.id="tests_done",0!==e&&(t.style.background="red"),t.innerHTML=""+e,document.body.appendChild(t)}}(t))}catch(e){E("mono_exit B failed",e)}Pe.exitCode=t,Pe.exitReason||(Pe.exitReason=o),!Ce&&Ue.runtimeReady&&We.runtimeKeepalivePop()}if(Pe.config&&Pe.config.asyncFlushOnExit&&0===t)throw(async()=>{try{await async function(){try{const e=await import(/*! webpackIgnore: true */"process"),t=e=>new Promise(((t,o)=>{e.on("error",o),e.end("","utf8",t)})),o=t(e.stderr),n=t(e.stdout);let r;const i=new Promise((e=>{r=setTimeout((()=>e("timeout")),1e3)}));await Promise.race([Promise.all([n,o]),i]),clearTimeout(r)}catch(e){_(`flushing std* streams failed: ${e}`)}}()}finally{Ye(t,o)}})(),o;Ye(t,o)}function Ye(e,t){if(Ue.runtimeReady&&Ue.nativeExit)try{Ue.nativeExit(e)}catch(e){!Ue.ExitStatus||e instanceof Ue.ExitStatus||E("set_exit_code_and_quit_now failed: "+e.toString())}if(0!==e||!ke)throw Se&&Ne.process?Ne.process.exit(e):Ue.quit&&Ue.quit(e,t),t}function et(e){ot(e,e.reason,"rejection")}function tt(e){ot(e,e.error,"error")}function ot(e,t,o){e.preventDefault();try{t||(t=new Error("Unhandled "+o)),void 0===t.stack&&(t.stack=(new Error).stack),t.stack=t.stack+"",t.silent||(_("Unhandled error:",t),Xe(1,t))}catch(e){}}!function(e){if($e)throw new Error("Loader module already loaded");$e=!0,Ue=e.runtimeHelpers,Pe=e.loaderHelpers,Me=e.diagnosticHelpers,Le=e.api,Ne=e.internal,Object.assign(Le,{INTERNAL:Ne,invokeLibraryInitializers:be}),Object.assign(e.module,{config:ve(ze,{environmentVariables:{}})});const r={mono_wasm_bindings_is_ready:!1,config:e.module.config,diagnosticTracing:!1,nativeAbort:e=>{throw e||new Error("abort")},nativeExit:e=>{throw new Error("exit:"+e)}},l={gitHash:"b16286c2284fecf303dbc12a0bb152476d662e44",config:e.module.config,diagnosticTracing:!1,maxParallelDownloads:16,enableDownloadRetry:!0,_loaded_files:[],loadedFiles:[],loadedAssemblies:[],libraryInitializers:[],workerNextNumber:1,actual_downloaded_assets_count:0,actual_instantiated_assets_count:0,expected_downloaded_assets_count:0,expected_instantiated_assets_count:0,afterConfigLoaded:i(),allDownloadsQueued:i(),allDownloadsFinished:i(),wasmCompilePromise:i(),runtimeModuleLoaded:i(),loadingWorkers:i(),is_exited:Ve,is_runtime_running:qe,assert_runtime_running:He,mono_exit:Xe,createPromiseController:i,getPromiseController:s,assertIsControllablePromise:a,mono_download_assets:oe,resolve_single_asset_path:ee,setup_proxy_console:j,set_thread_prefix:w,installUnhandledErrorHandler:Je,retrieve_asset_download:ie,invokeLibraryInitializers:be,isDebuggingSupported:Te,exceptions:t,simd:n,relaxedSimd:o};Object.assign(Ue,r),Object.assign(Pe,l)}(Fe);let nt,rt,it,st=!1,at=!1;async function lt(e){if(!at){if(at=!0,ke&&Pe.config.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&j("main",globalThis.console,globalThis.location.origin),We||Be(!1,"Null moduleConfig"),Pe.config||Be(!1,"Null moduleConfig.config"),"function"==typeof e){const t=e(Fe.api);if(t.ready)throw new Error("Module.ready couldn't be redefined.");Object.assign(We,t),Ee(We,t)}else{if("object"!=typeof e)throw new Error("Can't use moduleFactory callback of createDotnetRuntime function.");Ee(We,e)}await async function(e){if(Se){const e=await import(/*! webpackIgnore: true */"process"),t=14;if(e.versions.node.split(".")[0]<t)throw new Error(`NodeJS at '${e.execPath}' has too low version '${e.versions.node}', please use at least ${t}. See also https://aka.ms/dotnet-wasm-features`)}const t=/*! webpackIgnore: true */import.meta.url,o=t.indexOf("?");var n;if(o>0&&(Pe.modulesUniqueQuery=t.substring(o)),Pe.scriptUrl=t.replace(/\\/g,"/").replace(/[?#].*/,""),Pe.scriptDirectory=(n=Pe.scriptUrl).slice(0,n.lastIndexOf("/"))+"/",Pe.locateFile=e=>"URL"in globalThis&&globalThis.URL!==C?new URL(e,Pe.scriptDirectory).toString():M(e)?e:Pe.scriptDirectory+e,Pe.fetch_like=k,Pe.out=console.log,Pe.err=console.error,Pe.onDownloadResourceProgress=e.onDownloadResourceProgress,ke&&globalThis.navigator){const e=globalThis.navigator,t=e.userAgentData&&e.userAgentData.brands;t&&t.length>0?Pe.isChromium=t.some((e=>"Google Chrome"===e.brand||"Microsoft Edge"===e.brand||"Chromium"===e.brand)):e.userAgent&&(Pe.isChromium=e.userAgent.includes("Chrome"),Pe.isFirefox=e.userAgent.includes("Firefox"))}Ne.require=Se?await import(/*! webpackIgnore: true */"module").then((e=>e.createRequire(/*! webpackIgnore: true */import.meta.url))):Promise.resolve((()=>{throw new Error("require not supported")})),void 0===globalThis.URL&&(globalThis.URL=C)}(We)}}async function ct(e){return await lt(e),Ze=We.onAbort,Qe=We.onExit,We.onAbort=Ke,We.onExit=Ge,We.ENVIRONMENT_IS_PTHREAD?async function(){(function(){const e=new MessageChannel,t=e.port1,o=e.port2;t.addEventListener("message",(e=>{var n,r;n=JSON.parse(e.data.config),r=JSON.parse(e.data.monoThreadInfo),st?Pe.diagnosticTracing&&b("mono config already received"):(ve(Pe.config,n),Ue.monoThreadInfo=r,xe(),Pe.diagnosticTracing&&b("mono config received"),st=!0,Pe.afterConfigLoaded.promise_control.resolve(Pe.config),ke&&n.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&Pe.setup_proxy_console("worker-idle",console,globalThis.location.origin)),t.close(),o.close()}),{once:!0}),t.start(),self.postMessage({[l]:{monoCmd:"preload",port:o}},[o])})(),await Pe.afterConfigLoaded.promise,function(){const e=Pe.config;e.assets||Be(!1,"config.assets must be defined");for(const t of e.assets)X(t),Q[t.behavior]&&z.push(t)}(),setTimeout((async()=>{try{await oe()}catch(e){Xe(1,e)}}),0);const e=dt(),t=await Promise.all(e);return await ut(t),We}():async function(){var e;await Re(We),re();const t=dt();(async function(){try{const e=ee("dotnetwasm");await se(e),e&&e.pendingDownloadInternal&&e.pendingDownloadInternal.response||Be(!1,"Can't load dotnet.native.wasm");const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get("Content-Type"):void 0;let n;if("function"==typeof WebAssembly.compileStreaming&&"application/wasm"===o)n=await WebAssembly.compileStreaming(t);else{ke&&"application/wasm"!==o&&E('WebAssembly resource does not have the expected content type "application/wasm", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b("instantiate_wasm_module buffered"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null,Pe.wasmCompilePromise.promise_control.resolve(n)}catch(e){Pe.wasmCompilePromise.promise_control.reject(e)}})(),setTimeout((async()=>{try{D(),await oe()}catch(e){Xe(1,e)}}),0);const o=await Promise.all(t);return await ut(o),await Ue.dotnetReady.promise,await we(null===(e=Pe.config.resources)||void 0===e?void 0:e.modulesAfterRuntimeReady),await be("onRuntimeReady",[Fe.api]),Le}()}function dt(){const e=ee("js-module-runtime"),t=ee("js-module-native");if(nt&&rt)return[nt,rt,it];"object"==typeof e.moduleExports?nt=e.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${e.resolvedUrl}' for ${e.name}`),nt=import(/*! webpackIgnore: true */e.resolvedUrl)),"object"==typeof t.moduleExports?rt=t.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${t.resolvedUrl}' for ${t.name}`),rt=import(/*! webpackIgnore: true */t.resolvedUrl));const o=Y("js-module-diagnostics");return o&&("object"==typeof o.moduleExports?it=o.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${o.resolvedUrl}' for ${o.name}`),it=import(/*! webpackIgnore: true */o.resolvedUrl))),[nt,rt,it]}async function ut(e){const{initializeExports:t,initializeReplacements:o,configureRuntimeStartup:n,configureEmscriptenStartup:r,configureWorkerStartup:i,setRuntimeGlobals:s,passEmscriptenInternals:a}=e[0],{default:l}=e[1],c=e[2];s(Fe),t(Fe),c&&c.setRuntimeGlobals(Fe),await n(We),Pe.runtimeModuleLoaded.promise_control.resolve(),l((e=>(Object.assign(We,{ready:e.ready,__dotnet_runtime:{initializeReplacements:o,configureEmscriptenStartup:r,configureWorkerStartup:i,passEmscriptenInternals:a}}),We))).catch((e=>{if(e.message&&e.message.toLowerCase().includes("out of memory"))throw new Error(".NET runtime has failed to start, because too much memory was requested. Please decrease the memory by adjusting EmccMaximumHeapSize. See also https://aka.ms/dotnet-wasm-features");throw e}))}const ft=new class{withModuleConfig(e){try{return Ee(We,e),this}catch(e){throw Xe(1,e),e}}withOnConfigLoaded(e){try{return Ee(We,{onConfigLoaded:e}),this}catch(e){throw Xe(1,e),e}}withConsoleForwarding(){try{return ve(ze,{forwardConsoleLogsToWS:!0}),this}catch(e){throw Xe(1,e),e}}withExitOnUnhandledError(){try{return ve(ze,{exitOnUnhandledError:!0}),Je(),this}catch(e){throw Xe(1,e),e}}withAsyncFlushOnExit(){try{return ve(ze,{asyncFlushOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withExitCodeLogging(){try{return ve(ze,{logExitCode:!0}),this}catch(e){throw Xe(1,e),e}}withElementOnExit(){try{return ve(ze,{appendElementOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withInteropCleanupOnExit(){try{return ve(ze,{interopCleanupOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withDumpThreadsOnNonZeroExit(){try{return ve(ze,{dumpThreadsOnNonZeroExit:!0}),this}catch(e){throw Xe(1,e),e}}withWaitingForDebugger(e){try{return ve(ze,{waitForDebugger:e}),this}catch(e){throw Xe(1,e),e}}withInterpreterPgo(e,t){try{return ve(ze,{interpreterPgo:e,interpreterPgoSaveDelay:t}),ze.runtimeOptions?ze.runtimeOptions.push("--interp-pgo-recording"):ze.runtimeOptions=["--interp-pgo-recording"],this}catch(e){throw Xe(1,e),e}}withConfig(e){try{return ve(ze,e),this}catch(e){throw Xe(1,e),e}}withConfigSrc(e){try{return e&&"string"==typeof e||Be(!1,"must be file path or URL"),Ee(We,{configSrc:e}),this}catch(e){throw Xe(1,e),e}}withVirtualWorkingDirectory(e){try{return e&&"string"==typeof e||Be(!1,"must be directory path"),ve(ze,{virtualWorkingDirectory:e}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariable(e,t){try{const o={};return o[e]=t,ve(ze,{environmentVariables:o}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariables(e){try{return e&&"object"==typeof e||Be(!1,"must be dictionary object"),ve(ze,{environmentVariables:e}),this}catch(e){throw Xe(1,e),e}}withDiagnosticTracing(e){try{return"boolean"!=typeof e&&Be(!1,"must be boolean"),ve(ze,{diagnosticTracing:e}),this}catch(e){throw Xe(1,e),e}}withDebugging(e){try{return null!=e&&"number"==typeof e||Be(!1,"must be number"),ve(ze,{debugLevel:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArguments(...e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ve(ze,{applicationArguments:e}),this}catch(e){throw Xe(1,e),e}}withRuntimeOptions(e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ze.runtimeOptions?ze.runtimeOptions.push(...e):ze.runtimeOptions=e,this}catch(e){throw Xe(1,e),e}}withMainAssembly(e){try{return ve(ze,{mainAssemblyName:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArgumentsFromQuery(){try{if(!globalThis.window)throw new Error("Missing window to the query parameters from");if(void 0===globalThis.URLSearchParams)throw new Error("URLSearchParams is supported");const e=new URLSearchParams(globalThis.window.location.search).getAll("arg");return this.withApplicationArguments(...e)}catch(e){throw Xe(1,e),e}}withApplicationEnvironment(e){try{return ve(ze,{applicationEnvironment:e}),this}catch(e){throw Xe(1,e),e}}withApplicationCulture(e){try{return ve(ze,{applicationCulture:e}),this}catch(e){throw Xe(1,e),e}}withResourceLoader(e){try{return Pe.loadBootResource=e,this}catch(e){throw Xe(1,e),e}}async download(){try{await async function(){lt(We),await Re(We),re(),D(),oe(),await Pe.allDownloadsFinished.promise}()}catch(e){throw Xe(1,e),e}}async create(){try{return this.instance||(this.instance=await async function(){return await ct(We),Fe.api}()),this.instance}catch(e){throw Xe(1,e),e}}async run(){try{return We.config||Be(!1,"Null moduleConfig.config"),this.instance||await this.create(),this.instance.runMainAndExit()}catch(e){throw Xe(1,e),e}}},mt=Xe,gt=ct;Ie||"function"==typeof globalThis.URL||Be(!1,"This browser/engine doesn't support URL API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),"function"!=typeof globalThis.BigInt64Array&&Be(!1,"This browser/engine doesn't support BigInt64Array API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),ft.withConfig(/*json-start*/{
  "mainAssemblyName": "Corvus.Text.Json.JsonPath.Playground",
  "resources": {
    "hash": "sha256-u9hHK8I91OZpfNK/oldp+VK3xQRxt3mUpWgTRDPfpA4=",
    "jsModuleNative": [
      {
        "name": "dotnet.native.mx9wzm9o5h.js"
      }
    ],
    "jsModuleRuntime": [
      {
        "name": "dotnet.runtime.2zl32tp6ah.js"
      }
    ],
    "wasmNative": [
      {
        "name": "dotnet.native.imnhyiqpc9.wasm",
        "integrity": "sha256-2lvfACsds38yB7F9BvnIUtb0JBZIjimRTjlFpr4MLSw=",
        "cache": "force-cache"
      }
    ],
    "icu": [
      {
        "virtualPath": "icudt_CJK.dat",
        "name": "icudt_CJK.tjcz0u77k5.dat",
        "integrity": "sha256-SZLtQnRc0JkwqHab0VUVP7T3uBPSeYzxzDnpxPpUnHk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_EFIGS.dat",
        "name": "icudt_EFIGS.tptq2av103.dat",
        "integrity": "sha256-8fItetYY8kQ0ww6oxwTLiT3oXlBwHKumbeP2pRF4yTc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_no_CJK.dat",
        "name": "icudt_no_CJK.lfu7j35m59.dat",
        "integrity": "sha256-L7sV7NEYP37/Qr2FPCePo5cJqRgTXRwGHuwF5Q+0Nfs=",
        "cache": "force-cache"
      }
    ],
    "coreAssembly": [
      {
        "virtualPath": "System.Private.CoreLib.dll",
        "name": "System.Private.CoreLib.by5g8ahalq.dll",
        "integrity": "sha256-I4IOQI7Baodui+1cxSXmk0yEBQKHIj6hdEeolMFQHlA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.JavaScript.dll",
        "name": "System.Runtime.InteropServices.JavaScript.yj5rb6iqav.dll",
        "integrity": "sha256-3rdEKCIhKSF85/tYO+AQA9sAgg3Z57rOqAoaOxlP58s=",
        "cache": "force-cache"
      }
    ],
    "assembly": [
      {
        "virtualPath": "BlazorMonaco.dll",
        "name": "BlazorMonaco.8amkbtt9ah.dll",
        "integrity": "sha256-0DwAMDShv0IXbPeW9PueG9CcLW9Q1GEhOIKnImY6joE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Corvus.Text.Json.dll",
        "name": "Corvus.Text.Json.9mv1l7oom4.dll",
        "integrity": "sha256-UYdbQuH8p5EO97FLBK1/tWneN1uZE2WyOcyDsTqv2NE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Corvus.Text.Json.JsonPath.dll",
        "name": "Corvus.Text.Json.JsonPath.95bdeuhqsi.dll",
        "integrity": "sha256-U/Bp/yDBGvyZDVnCBInkr8DwBGTVGX/oXEB7GmYvYLs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Corvus.Text.Json.JsonPath.Playground.dll",
        "name": "Corvus.Text.Json.JsonPath.Playground.lvtad88mvl.dll",
        "integrity": "sha256-P/2AlwgXakuXBugaek3aTgzw2KBQzdOnddpgudQobEo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Humanizer.dll",
        "name": "Humanizer.umqku5jypw.dll",
        "integrity": "sha256-1oUwdVR9fjA+/WA1TZEaL/GO26WCzaL6WdkaLl3Pnpg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.Web.dll",
        "name": "Microsoft.AspNetCore.Components.Web.myn3qp44b5.dll",
        "integrity": "sha256-8sWUhaP2o2bKmVqfL1/8WVwjNoeadSMN3ZAhIazCKKQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.WebAssembly.dll",
        "name": "Microsoft.AspNetCore.Components.WebAssembly.ckxld1y7k4.dll",
        "integrity": "sha256-1LHgOw9l5s34SF6Bo4Jl7KXmYH3/j3yTOyanA7KjEFk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.AspNetCore.Components.dll",
        "name": "Microsoft.AspNetCore.Components.rin7fnjmfm.dll",
        "integrity": "sha256-LbIGckQSrGrXJQBwjeJcUKuBE/kKBDt85YWX51Oz/Rs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.dll",
        "name": "Microsoft.CodeAnalysis.CSharp.Features.fpz8vutpbk.dll",
        "integrity": "sha256-PdLrPHKivueXst7NKSjOH+E44fhOLrVsmv3SxyovReE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.dll",
        "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.9m7yxwmkn5.dll",
        "integrity": "sha256-7wNCSQXf/uGMDKxRiuQraHIPb0BNs4m87XKmLMZ3bsY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.CSharp.dll",
        "name": "Microsoft.CodeAnalysis.CSharp.obbod6kesa.dll",
        "integrity": "sha256-VBQncKAOWUlC7okjcm6kkSYwPQxSOTKIbCQX62bcB/g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.Elfie.dll",
        "name": "Microsoft.CodeAnalysis.Elfie.62gdi7eyhm.dll",
        "integrity": "sha256-jsDhisVwFXs4Y/cANbZEkl2JmWrZAfMe+RjSsPdCiUE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.Features.dll",
        "name": "Microsoft.CodeAnalysis.Features.57o4z70qpl.dll",
        "integrity": "sha256-f63BoBeNvGeCjmwXJqK3ZBAyA2K1UZjbPvZwDTc1mS0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.Scripting.dll",
        "name": "Microsoft.CodeAnalysis.Scripting.ln8qw0ccz4.dll",
        "integrity": "sha256-r3HmFyTqDn6v+NNxUYHPs/6QFFs0AmCalFewae5gpMo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.Workspaces.dll",
        "name": "Microsoft.CodeAnalysis.Workspaces.hm3rwxdrqw.dll",
        "integrity": "sha256-5+DamDRHukGWToUfFj1hRP7tYK1o/0+nLPAyE9SFC1A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.CodeAnalysis.dll",
        "name": "Microsoft.CodeAnalysis.t0tf1lj017.dll",
        "integrity": "sha256-rYV/HOf44mwnIq3nBfrD+QUCpHWQwUB13xMnIQ8Qse8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.DiaSymReader.dll",
        "name": "Microsoft.DiaSymReader.zjf3t0xi2e.dll",
        "integrity": "sha256-fz51QzIf9MJnW5TlakoyyTXBHIL8xg1PbLYRywqNqtw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Abstractions.dll",
        "name": "Microsoft.Extensions.Configuration.Abstractions.4laejdwpf5.dll",
        "integrity": "sha256-6EuLaiHwo125319gSNVSjO8wTsCUuTeWhDNTsKpt5t4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Json.dll",
        "name": "Microsoft.Extensions.Configuration.Json.jiib2jr2lw.dll",
        "integrity": "sha256-e9DduyiDn2YYq+yYToKkOsDwTMDW3qYFp2BfnLPzvkc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.dll",
        "name": "Microsoft.Extensions.Configuration.a7cqb811ax.dll",
        "integrity": "sha256-RhvNF3ceSH4xO/gAE31V8YYoBplwoKuppeVDoBQ2ADo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
        "name": "Microsoft.Extensions.DependencyInjection.Abstractions.n9yxqsyrno.dll",
        "integrity": "sha256-Rar7JdwjaF6Wx0vS3qD/S71zt7L3IX/wb8xxAUrmh5U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.dll",
        "name": "Microsoft.Extensions.DependencyInjection.wb8iqzslmd.dll",
        "integrity": "sha256-rMBmR8mUgn0ra2JrsgUi7mPedgVV36yyG00fWKYij28=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Abstractions.dll",
        "name": "Microsoft.Extensions.Logging.Abstractions.xogrusuyh5.dll",
        "integrity": "sha256-v0T0Px+V/674BkvBX02XE7SZdiUjoDhBQVIT2wvP4l8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.dll",
        "name": "Microsoft.Extensions.Logging.30cgta7vlt.dll",
        "integrity": "sha256-w3XJE1BeRyx55zToM5KPazdF0U11W7xGiX4QRB2nXrQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Options.dll",
        "name": "Microsoft.Extensions.Options.0cvozjb2ig.dll",
        "integrity": "sha256-oBMxjWcLaFZWE3ao1UQOU5JF6hz6+uOr89tAdYKtSzo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Primitives.dll",
        "name": "Microsoft.Extensions.Primitives.1g42lorify.dll",
        "integrity": "sha256-AUhZPtJ+ylgSXy2WurZVmvVwVuqGh08FX8GtgUcxthA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.JSInterop.WebAssembly.dll",
        "name": "Microsoft.JSInterop.WebAssembly.4tnlozjtat.dll",
        "integrity": "sha256-yF/0FTfKhT1Y3QTdfMX6hBtYqSaaL0C2FSDkdWpMorI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.JSInterop.dll",
        "name": "Microsoft.JSInterop.a31imsj4zt.dll",
        "integrity": "sha256-BswILdGPt4s5FPI73h5jpVkIjd04yPtTw0xBLceNi3A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Win32.Primitives.dll",
        "name": "Microsoft.Win32.Primitives.h2lpm8f2nt.dll",
        "integrity": "sha256-F2PblRAA2aLRSL/WRBjXSH0PCzayAC5qCl3/jsDt7vw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NodaTime.dll",
        "name": "NodaTime.ccqfkxbchp.dll",
        "integrity": "sha256-5GQviUKl4aKSvqMPS8q2B02RoB+MFk+LoK+9PBJfyZw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.dll",
        "name": "System.3iexjuxwra.dll",
        "integrity": "sha256-Vf1VURbSEf/otVLjKVxGajAUREQ0T8BtNhcLkY3Ybtc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Buffers.dll",
        "name": "System.Buffers.1b727tuyh3.dll",
        "integrity": "sha256-fewOV3RnigFUUYxj/olh8ty4SNOwV+cECD//LoAO1GE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.dll",
        "name": "System.Collections.njjskobq54.dll",
        "integrity": "sha256-DYo+4hkxZqihTwsq/DwDihCBuOSyJ7goSWfsZsTAEw4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Concurrent.dll",
        "name": "System.Collections.Concurrent.ay5ttdpxzb.dll",
        "integrity": "sha256-YiSUB3xf00MoGTrxvSJfLwr5gcil7u7ZuDoxWiEKeeM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Immutable.dll",
        "name": "System.Collections.Immutable.3gsb9uerj9.dll",
        "integrity": "sha256-PRr3IOMx5tvCBuIKuDsof+xs5MV9uosLh8xfsO0xsQo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.NonGeneric.dll",
        "name": "System.Collections.NonGeneric.k0vdkvrfi9.dll",
        "integrity": "sha256-vEEZAex97LW+i6ATH63Vk3MgRcCMDujniUK9nUD5ghQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Specialized.dll",
        "name": "System.Collections.Specialized.o10bje8yv9.dll",
        "integrity": "sha256-/L8DhZ/6mdVE/StrVZuxcrGbh7dzDIyjVxVE+TqXIMk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.dll",
        "name": "System.ComponentModel.4xaiyxhy5v.dll",
        "integrity": "sha256-OOKm0iypjXbEJW9KXazH0KDOvizfSnrFSPHJ/+fcn60=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Primitives.dll",
        "name": "System.ComponentModel.Primitives.bzjp66jp3f.dll",
        "integrity": "sha256-Zz/9W+H1XM4dl/Jmai7h/wfrbi/uRD7WV1i/jK3JaCE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.TypeConverter.dll",
        "name": "System.ComponentModel.TypeConverter.5vxgzocnol.dll",
        "integrity": "sha256-cfJUZSn4oT0ceDnbHahaXowKKJysf1dHcZRAYD8Fpo0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.AttributedModel.dll",
        "name": "System.Composition.AttributedModel.ldir8ln0ls.dll",
        "integrity": "sha256-N35w7x6v84DeQ9XpZ18L6yDH7f47SabN1IMrxfpCjQE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Convention.dll",
        "name": "System.Composition.Convention.yn2xya4od7.dll",
        "integrity": "sha256-vpX0gESasyNjFb6zIc5ZimE4LWRHnd9QDnQ0Y3NF9HQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Hosting.dll",
        "name": "System.Composition.Hosting.g3n967vez6.dll",
        "integrity": "sha256-LI8Qkjw7YP0bFtifJmFxKpvhEuaXmQvtvW+rnbqrsbY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.Runtime.dll",
        "name": "System.Composition.Runtime.ugsns9czpd.dll",
        "integrity": "sha256-3s/CtgZGV6lp78LUOVKWW0ZWy7TDYQD7HtIpx1/TjTQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Composition.TypedParts.dll",
        "name": "System.Composition.TypedParts.n5udoeucat.dll",
        "integrity": "sha256-68lCw1jZl+QWNcmjZdtUKj/SoF8wH2AtXWqLDV3UIHE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Configuration.ConfigurationManager.dll",
        "name": "System.Configuration.ConfigurationManager.nuhqtoc5j6.dll",
        "integrity": "sha256-33lQJfIHtJQOyaSN7drtqzMzsDGxUwP9pPFIsJxFbW0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Console.dll",
        "name": "System.Console.zm0x3p37ma.dll",
        "integrity": "sha256-haeib7X79+jD5+qHWDOk4QQo3Cr0UsF/lqtnroZ6O5M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.Common.dll",
        "name": "System.Data.Common.t3q0b6j85c.dll",
        "integrity": "sha256-14igNen6/mf7h9lax05hsYaZqnt9JfpHxLfF6f56giw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Data.DataSetExtensions.dll",
        "name": "System.Data.DataSetExtensions.m1e36qarvn.dll",
        "integrity": "sha256-Jsh8aOB8QMeiPfAPH2xe9UpN7/9LdlRWpHg8RcEsJic=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Contracts.dll",
        "name": "System.Diagnostics.Contracts.t8i848pu9q.dll",
        "integrity": "sha256-oz0p0GFQY9nmAVTcZdQK6xfdSUHF6ETo+mzvo6ijvXA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.DiagnosticSource.dll",
        "name": "System.Diagnostics.DiagnosticSource.0ud8gee5gg.dll",
        "integrity": "sha256-+N8A9iV/wkWEW9Nd+a7AI/3zN20t7uOxsFP2MdMad7A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.EventLog.dll",
        "name": "System.Diagnostics.EventLog.hc2p429rnf.dll",
        "integrity": "sha256-3wsXjGSkdfqiGNYNs4FlbSs7KiSs319UkGVyr7K30UY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.FileVersionInfo.dll",
        "name": "System.Diagnostics.FileVersionInfo.ltxhtfw3di.dll",
        "integrity": "sha256-eV7V9ZCP90FO4xvn2MNrL5vs2m8xeFk9IfwDuf+MFGg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Process.dll",
        "name": "System.Diagnostics.Process.hfyqmolkqq.dll",
        "integrity": "sha256-zbYhp68nlB4fmfgPS/EX69dVgIS+ZsGDtvIGkzKfqHc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.StackTrace.dll",
        "name": "System.Diagnostics.StackTrace.6wnfelxfod.dll",
        "integrity": "sha256-Ssfr4/VG2uaHBiyRjMu1PBQXZNrydm/B1YPPCQcP+XU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TextWriterTraceListener.dll",
        "name": "System.Diagnostics.TextWriterTraceListener.6w8ri99tfv.dll",
        "integrity": "sha256-OrQfnW9z0Jzy/Blvov3lJdqA9kALRbbMVHtANWn8OoA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TraceSource.dll",
        "name": "System.Diagnostics.TraceSource.dqrgy1kbeo.dll",
        "integrity": "sha256-ZfZFM11Wm2l8jJ8QwOvrHjS5GNJhmNpg7nBKcH0R/Ts=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Tracing.dll",
        "name": "System.Diagnostics.Tracing.oec53x36b2.dll",
        "integrity": "sha256-8HraNiSxfNDKIuHRYkgy853NHEJbEsnTmK1hQndScks=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.dll",
        "name": "System.Globalization.qalc9dvq81.dll",
        "integrity": "sha256-0mNrGgkkWgsdEfEMnpiEH9SU2N8lMxNvcBZAiif4sH4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.dll",
        "name": "System.IO.Compression.ekt0ofk6i4.dll",
        "integrity": "sha256-rgmcbIiR48Is2zLuGLqKkHDwMNNU7l/thXRA/aT3dqA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.MemoryMappedFiles.dll",
        "name": "System.IO.MemoryMappedFiles.hgpp9zb8my.dll",
        "integrity": "sha256-3+3sHYMb0dbiXDav0TN11Br7KoFAV9cC8s/Ezy6rpYU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipelines.dll",
        "name": "System.IO.Pipelines.hfjrjucyzj.dll",
        "integrity": "sha256-4/nNnS/otybdJF2y+OfDiJ+UWklkysCxPYYoRka99l0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Expressions.dll",
        "name": "System.Linq.Expressions.oulv7xpvcv.dll",
        "integrity": "sha256-YFtDk0oJaJZjloL3Rl0r4lv2F59cJC7NNJ14XOw88Is=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.dll",
        "name": "System.Linq.zxuvzn4p4r.dll",
        "integrity": "sha256-Z+38YoFKJPJ/omzll/TUnVUV90sjMj60AO7M7wEIICo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.dll",
        "name": "System.Memory.jatpi0savb.dll",
        "integrity": "sha256-dQ2RRwO/FNuC1hevzCU78X/Hp3ZAcABlK2EwAGawH1s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.dll",
        "name": "System.Net.Http.7tdgsb20v0.dll",
        "integrity": "sha256-FY6EvJnIBrSU0JFA9qatyTmv0MaVmKFn7ieIvCGOAXY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Primitives.dll",
        "name": "System.Net.Primitives.e9hec9kpeg.dll",
        "integrity": "sha256-HuFSFjzyJYPZ4uKQ6N+GYmyN8Jcr44sQ6p+oPeI5f/M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Requests.dll",
        "name": "System.Net.Requests.cmniq0ims0.dll",
        "integrity": "sha256-7L1zLNo+LR6IN+0CxVWl+L/PK3tHlCVjtwE1UMYdhAA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebClient.dll",
        "name": "System.Net.WebClient.80n7ocfbwv.dll",
        "integrity": "sha256-OOgGJG7patVUgKL7ltFtE6B+T75LeZNNdoBqQVhRnA0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.Vectors.dll",
        "name": "System.Numerics.Vectors.mlsla89o58.dll",
        "integrity": "sha256-Cn73e5nVGPVCcv7BHbWKqkPzpJnM1PHVGHIoU70oDpk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ObjectModel.dll",
        "name": "System.ObjectModel.gsaee3yiab.dll",
        "integrity": "sha256-4Hd48qDLTSvD+MUR0U9CM0MEkLXuk6+WxmmNbf6bYPE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.DataContractSerialization.dll",
        "name": "System.Private.DataContractSerialization.p5c6byqz5e.dll",
        "integrity": "sha256-DZavxDDek1kFZ7MoMmpZB8QwLaxNiRQlB56fYXnPiDQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Uri.dll",
        "name": "System.Private.Uri.i5i28qg1s7.dll",
        "integrity": "sha256-ZrhaoInj45xnuE+yaNe1PA7WGMh+WDyyvEgGHAA9vXA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.Linq.dll",
        "name": "System.Private.Xml.Linq.tz897qfi5o.dll",
        "integrity": "sha256-N+u5F6SrZBXj755wmv7A6gHRWILaTiaWgFkdBZ76pkk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.dll",
        "name": "System.Private.Xml.hdjs7u01hy.dll",
        "integrity": "sha256-689BWAjpqgbWiATECaWawzmEA1d7taywDfXlCevWxic=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.ILGeneration.dll",
        "name": "System.Reflection.Emit.ILGeneration.parpgy1mlg.dll",
        "integrity": "sha256-cb5zJg/4Usdauo+Ry6ZRwEFF/ezYL5XfPafaSrD4UNM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.Lightweight.dll",
        "name": "System.Reflection.Emit.Lightweight.7q8xr05znr.dll",
        "integrity": "sha256-b7VsENGHFfgBqxpsVkpurtiua0n/9VeydgxDZLx5LQU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Emit.dll",
        "name": "System.Reflection.Emit.o5x1x1yr4v.dll",
        "integrity": "sha256-HKOaL6yT72wKAxB0cXblFQRi4AMf3nzWqiiBAuAiNMU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Metadata.dll",
        "name": "System.Reflection.Metadata.zqt7km9345.dll",
        "integrity": "sha256-pXG1h7XXhOHsEZtcRCV0Ad1fG93pwkanbwY432KnhCM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Primitives.dll",
        "name": "System.Reflection.Primitives.2v64ihgopv.dll",
        "integrity": "sha256-Qs7ORzw8PmfL4lqDLhEUwmCXzqtaWNNBFzkrej/jzuA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Resources.Writer.dll",
        "name": "System.Resources.Writer.lmua8ufp9b.dll",
        "integrity": "sha256-c9YAMaNVHTq5wsIwvlHBTNoT4/hl2GGzFjjV4LkwhHY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.dll",
        "name": "System.Runtime.InteropServices.f4m6gij1zj.dll",
        "integrity": "sha256-f72p1+uj5GtnoiCzR9uUTNiDL52mDjov1vuXAD1jjJc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Intrinsics.dll",
        "name": "System.Runtime.Intrinsics.7tv652mnzo.dll",
        "integrity": "sha256-i3PggTuwmid5X+xJjMSsxVAD9GDFPp3zFDCDWf2q02w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Loader.dll",
        "name": "System.Runtime.Loader.49mkhzi3rx.dll",
        "integrity": "sha256-qKpfk+YlaPl/rt34D+GhavxQytsaMeDV6p7A8lM4MRY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Numerics.dll",
        "name": "System.Runtime.Numerics.02qvtdf8pt.dll",
        "integrity": "sha256-qBapygQxLHZz28PVRk4lyFjQ0350CSJgZSlZEvH6Tv0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Formatters.dll",
        "name": "System.Runtime.Serialization.Formatters.o85gjejel4.dll",
        "integrity": "sha256-iBkiKwY8O55Meg6ALcfXunJSB0Qx5LPTcaABQ6twoKE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Json.dll",
        "name": "System.Runtime.Serialization.Json.oc6nwigf0g.dll",
        "integrity": "sha256-6OfqKf4lid4oLvuaRniNURWZ+ufkLz8mypYIkx23nSg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Primitives.dll",
        "name": "System.Runtime.Serialization.Primitives.arlkfen7lb.dll",
        "integrity": "sha256-JkJDW6T2AKoURUe4Dt0Xx7B0jQZ9MhmMJg34WQt8GWI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Xml.dll",
        "name": "System.Runtime.Serialization.Xml.o8ki9knqml.dll",
        "integrity": "sha256-+J+PyiIBqwQ6wvVENgsVZAVYJFUHnzh+qH6GSdaRInE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.dll",
        "name": "System.Runtime.rhfvon7d66.dll",
        "integrity": "sha256-0RudVKiccFmpVVcQaSU+a+DAem7NrdmCfuttN5Kpx1Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Claims.dll",
        "name": "System.Security.Claims.y64i3bd17y.dll",
        "integrity": "sha256-uuSvcTIrARks4E4p2TtPV6xnuz1vcalncyaNGKxxNwY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.ProtectedData.dll",
        "name": "System.Security.Cryptography.ProtectedData.ru4yev6rek.dll",
        "integrity": "sha256-ExeCHQYsAfByFa6oHkq764YJL+uURz0yQUUgrpoBYL8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.dll",
        "name": "System.Security.Cryptography.0agfuuxqk1.dll",
        "integrity": "sha256-2FlZKcy03Rj4DzK+XOD5ORIeTRdUZcZnvumUN1GFUR0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Principal.Windows.dll",
        "name": "System.Security.Principal.Windows.bfq9ks5if4.dll",
        "integrity": "sha256-+b2zhUO9bHWyuwPDAHxpP914L0BeLQPmMVNCqNUJsXo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.CodePages.dll",
        "name": "System.Text.Encoding.CodePages.bggn5x182n.dll",
        "integrity": "sha256-9fMw3CmhQw+TsELfAJKWxTF/hA5JUFj0PfWegFzOsT4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.Extensions.dll",
        "name": "System.Text.Encoding.Extensions.ytsl5ayue6.dll",
        "integrity": "sha256-yk1E+U4q/4yhINTvIBwcVOg6wXTOWIoIMev/Yr+hdCI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encodings.Web.dll",
        "name": "System.Text.Encodings.Web.wdr6j745ks.dll",
        "integrity": "sha256-JL+NsWgZlxlJvzUNa7LtxgeDtzit6+uAsqbBJUKCSew=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Json.dll",
        "name": "System.Text.Json.u1803olz14.dll",
        "integrity": "sha256-wntselx9HRUXsNY1GtQDE3hVoLETqUA1KbgjXTedJYk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.RegularExpressions.dll",
        "name": "System.Text.RegularExpressions.uep4asaxxo.dll",
        "integrity": "sha256-Wow0RtmEvPL0kg/N6tClYpP2QbYUCoobss1Z0Og/Wy4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.dll",
        "name": "System.Threading.s3du79wflx.dll",
        "integrity": "sha256-2Ih5hAcGoeVGR+lerDZlaqazLEPnkm/4EPaz/q98sao=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Channels.dll",
        "name": "System.Threading.Channels.nid965maky.dll",
        "integrity": "sha256-r4arRGjJ/MxSeVQomZk0NJWKZV4zR2ZqaykaH411G5E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Overlapped.dll",
        "name": "System.Threading.Overlapped.d6bl3p17dx.dll",
        "integrity": "sha256-Kx1m4PNIo6b4CIhy6Eic9A3qv10twDjjKvT3z0VeOSk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.Parallel.dll",
        "name": "System.Threading.Tasks.Parallel.gahbibibma.dll",
        "integrity": "sha256-uJVLStyj542PTIA+2JoBH9DF2qPoAeaxDjvy52K+LHU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Thread.dll",
        "name": "System.Threading.Thread.vlk52ry2ip.dll",
        "integrity": "sha256-kyVp1J7UG1Zeon/xMvFw5EUQ3euZfWqsm7E2jIaxYEc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.ThreadPool.dll",
        "name": "System.Threading.ThreadPool.1hac2a5cs8.dll",
        "integrity": "sha256-Bbrozd40mBtOxEGFMip6zL53ng+F8EuUzMpAldX8krc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.Linq.dll",
        "name": "System.Xml.Linq.rn7xmq4t5e.dll",
        "integrity": "sha256-+Vxp6K3Rht6mQsp9/RPWZwXSkFPYAZudLM1VqcZ1XeQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.ReaderWriter.dll",
        "name": "System.Xml.ReaderWriter.nk2hdfesf6.dll",
        "integrity": "sha256-uS/eblPfh9myU1syWJpmUVp2hH5OdPVh79+y5EZVNYA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XDocument.dll",
        "name": "System.Xml.XDocument.55oog3l1j3.dll",
        "integrity": "sha256-ORxltYpQ5jVzCyTq6dO/pSuGkJpuqaXxDMVxfL7zqgo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.XDocument.dll",
        "name": "System.Xml.XPath.XDocument.z8lg0qmxqe.dll",
        "integrity": "sha256-kxfUmbAozZ13O3bs731WxXEw2xvGHZ8oy0Yy1slsewA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XPath.dll",
        "name": "System.Xml.XPath.gxfzf2yvr2.dll",
        "integrity": "sha256-ZNKLS2+56PIe3rOfyWhtW5Q4mGAOjovYZlu1sr7G2q8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Xml.XmlSerializer.dll",
        "name": "System.Xml.XmlSerializer.j2wmbu9drh.dll",
        "integrity": "sha256-JcHm3/xeoQCXxNMBKmc7vv9DDDRq1X3PVM3QI0Pf9FY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "netstandard.dll",
        "name": "netstandard.wi4f1va7v0.dll",
        "integrity": "sha256-qKHH734oasxq7DQ8opiZjGz4vOnOkC1kr1HlCNO38+E=",
        "cache": "force-cache"
      }
    ],
    "satelliteResources": {
      "cs": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.qmbxzkcawd.dll",
          "integrity": "sha256-Isx0tDR0WwlkMXVekEWknWLxg7i7hCc87rMuSDCFkvE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.j55vid9s2i.dll",
          "integrity": "sha256-12E+XefHX1cykSniCAIbEUOgg1gmyXUc5pBhV65udtk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.65n57obgrc.dll",
          "integrity": "sha256-loQUBqj//cfk+CBIk561GT0OU0VBGTaKToJmvd3+3qM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.pb4bdga01v.dll",
          "integrity": "sha256-Wwg9XiTKAHej81BSPu2T0cSA2ScUC7f1/xXASOdqg+s=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.lod3jadc6o.dll",
          "integrity": "sha256-+2ynljycHBiAw0lH3jAYwbsCJSohQ78lUfqUYzrGpeU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.5lk8458pxu.dll",
          "integrity": "sha256-x20o6b3T1xmegcnbwqrLcChugj/4HDSBw2oze894wJg=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.uaw4s2y3o9.dll",
          "integrity": "sha256-hkC9Bad9Axw+EGuYo2jk9RYY4qrPECf54OKLJgmXg58=",
          "cache": "force-cache"
        }
      ],
      "de": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.zrzwofbq2l.dll",
          "integrity": "sha256-H2FsEi5HxnAX5PI7d4DwKPPvU/xi4kgnoPbIlGY+vlU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.qfeinszmxu.dll",
          "integrity": "sha256-as8ZAYkyyAvzWlFvLrn+BW2/5kLT9ZoivZLTuo7W4GM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.mugvdr7vzl.dll",
          "integrity": "sha256-8u8SquSv90ntWWMHXahqqP097EOJDl/XsAvCJ9qtKLY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.0oxbuaegub.dll",
          "integrity": "sha256-0FrL3NcC2VKZ83QGgR/WqkL0rzVM8OiVgrDCb25JbBM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.ipm7fif8t9.dll",
          "integrity": "sha256-SqrlI30USrH/Qrxrj9mAZodiWOiA4aeG3mlUcj2FOGY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.9tctve44a5.dll",
          "integrity": "sha256-42L4rO/x3+qe358kli3xrQUNL3RYRwAM/rifcczHmqw=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.s6i2e8yg8z.dll",
          "integrity": "sha256-lNZ1f7OdY3QIyuAFaXeaWO1bqRz+F0hLfZw3teaG6oU=",
          "cache": "force-cache"
        }
      ],
      "es": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.gwipwvjihh.dll",
          "integrity": "sha256-ECgMfvyZQgfqo6n2HJbdFH+diLG2+EHGO2+NzxSA/Dk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.ams9woqair.dll",
          "integrity": "sha256-Ipmu7AL6vr8H1KNpy22dJd5zfmaGWlMLFwyV1ZZ8KF4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.yjbpb7yvrf.dll",
          "integrity": "sha256-vk3H9dK2wHRgHUrPFAQ1D61AGW2O2wpOPQTVnqPdUw0=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.3lxjvv47ez.dll",
          "integrity": "sha256-OXSzsaoiZZWtRhvzbIUImxuOhXTQaagrUEStANtwzI8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.31t5emkiqc.dll",
          "integrity": "sha256-N8gc6yLprk1BMlHkP/gWkgg6FzpdcmLdv/r2ZQIY4TU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.fu5zczo4q9.dll",
          "integrity": "sha256-V4Dhdsm+AjoXfAedQquN7bxRFjKRgdiOUcixTF+vh/E=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.rb8tohm9ix.dll",
          "integrity": "sha256-mZQNGHEFSuW72DHZP4J78/Rs+sE+xjfWML5EkM6A5Tc=",
          "cache": "force-cache"
        }
      ],
      "fr": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.tpadrt9zk7.dll",
          "integrity": "sha256-f6mjYufo1n/nvv/ZjiFJVVAeSMdGMhv+NAuaFd1Sf6s=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.7asn191eqr.dll",
          "integrity": "sha256-ERwJ4sirDpO44jLKHIJ1nHt2hGGgkiBIdKTN5f50vjE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.r8p0pz3mcg.dll",
          "integrity": "sha256-y1SoV559haF+CLtysaZ1Jk7pL1GwCh1emjuINn2oXvk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.l1x8ogaaku.dll",
          "integrity": "sha256-SdF4L8DzdiQVmko/otD/qzqI9359N6+5cUjkfb7exb8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.99w7rlxv5d.dll",
          "integrity": "sha256-8yFzbw3AzJDCFq5pu6MUN8cWJmnFfFuJOiTUhqpxids=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.jlpla7q5rt.dll",
          "integrity": "sha256-1xrYaj7SCpZ97QLFOiRT0IlUbMb1ch6Ipobj8YFy/7w=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.0ktb2aauzh.dll",
          "integrity": "sha256-YDTv+4IWTEMDDwRSQAtTfYlVsemoS3rnbl96H3/s9yg=",
          "cache": "force-cache"
        }
      ],
      "it": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.2li11tp3pd.dll",
          "integrity": "sha256-VpmPK+FTTvo50v/AT5xxPaSjKc3Vz7WuYCZMaChy0Qg=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.lv2anvys5u.dll",
          "integrity": "sha256-71+l19EBoe3CYgim3Rmz8bKMG+Io+yEU6REsiPMwxRM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.gy90yiqytk.dll",
          "integrity": "sha256-aDyAi+nv1sh85qfeBtf8jfFSMk2z1yvc/84gmjgqda4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.7v7galauks.dll",
          "integrity": "sha256-LZZOMqN2So3J8em+uEo2O5OTalCP5gWhEaWkC1ojGok=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.yzh871k4wa.dll",
          "integrity": "sha256-HlgCnPdBIHBbWU1BfGneFuDWVewr8DToglol5T8iiQ4=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.t3j3941v9e.dll",
          "integrity": "sha256-eYzttf5nnp96DPR7p0BIaSoEo4SLv18BUot+9ZuHt+M=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.5bkwbd06ms.dll",
          "integrity": "sha256-0e12/SP6w+A7py67ZrEp99wncc352LpEGy3UBG3Mt2k=",
          "cache": "force-cache"
        }
      ],
      "ja": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.peblhcp2si.dll",
          "integrity": "sha256-AZ5rlFPblQZUXr2er3r5QIya+EVYwQ8YKpRZw03ulxk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.x5269nifr5.dll",
          "integrity": "sha256-i2pMCJSw7suJFaSwY1QYB4cErjIMJ78qpG4H7t/al+I=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.3gyziivzwi.dll",
          "integrity": "sha256-3dwbXkpUkmmeZlltU5jjunrv8MGkQXuu3lpE+EGfiWc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.20nueiahew.dll",
          "integrity": "sha256-Dvrco3jnFIbycMvXikQ95tKSgQHkEeLWquMYKM/pjIw=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.bqublycray.dll",
          "integrity": "sha256-U+1jLTP6Tk8KtHebDUPlDo9hThzjSTC10es/EDxSfsA=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.dinjkgm8mv.dll",
          "integrity": "sha256-xTGIQ3NRGiEgzipnWMd3YCsxNwc5fDWKQuJ4QYmsvSQ=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.mol16k8uj8.dll",
          "integrity": "sha256-Brr2iu7uju1qbW6M6QNu8+3P6xLeh2WD7duOrsYNsrs=",
          "cache": "force-cache"
        }
      ],
      "ko": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.k45qd4zyuc.dll",
          "integrity": "sha256-dAnMNAVXYmR7SMLzRclNzA8CWweLnb+hbzfVv9N+8QE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.aszcq3k3zo.dll",
          "integrity": "sha256-1nnx3ijexarE6TECMYl9mo8wem2Tq4ULBZiaKCA+UEk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.f1xugstzjv.dll",
          "integrity": "sha256-PW3vtc4H/RfLAMpWM+V6OmXlq3lncy1QavGhbgnwuqA=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.cdmgf51ofd.dll",
          "integrity": "sha256-wHeWTUyEU3P1dxZXW95pZvvZPEuEo+1h77UT7XSJFNY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.gswzf7dpuw.dll",
          "integrity": "sha256-QHTV5IhzOUKCIkaI/dpXRdraDJmZuiA1zmdbK7F+nI8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.0l7aw6q2gs.dll",
          "integrity": "sha256-5FzPVE6KorUkzOhrnaCXEnYms1BG1zBTWfAd3iKddKg=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.fw35399350.dll",
          "integrity": "sha256-AWb4DxpV2UfKQVHLcPNvDOrTn60fz2SOviQZw94flTg=",
          "cache": "force-cache"
        }
      ],
      "pl": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.i5op6s97ph.dll",
          "integrity": "sha256-hkzOk5A+3QQRTQIjO6UtwQ2N9sm+ygrAYRXBJb3H2+k=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.flj7o6mpji.dll",
          "integrity": "sha256-8+62r441CAFQkX04tVf3DPZjVUdZBZWODMhLh6nj90k=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.hyhfwwxee9.dll",
          "integrity": "sha256-qTh+wxVxOn4yhhQlFZlUk55hObbe4XH+t4WyfR0xyoU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.ge5psyckx4.dll",
          "integrity": "sha256-mAvsynYe/EIXfthHy7OKcJHTkbb1BmLvGFZzQRf9LuY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.jilt6ptxst.dll",
          "integrity": "sha256-1RMcABEFvCvDpii5+puVvYNZxS/KLX/i3X5CfMHdqkM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.lb380pbt89.dll",
          "integrity": "sha256-0bbOF/vtGVQZrFEyZmrJ6jKGE6V0SglgdFRpnCUizDE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.cj4c4ixvth.dll",
          "integrity": "sha256-+H0qSV2JGGhpbiTHJdLEgHeXV99+O9IrxmrpJyKPGBo=",
          "cache": "force-cache"
        }
      ],
      "pt-BR": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.iqmbm08zb4.dll",
          "integrity": "sha256-2l2CKXTuqC4bu55HzdQNXZQDccDEZjQaSKMPXoNLugM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.7cztmtm8yq.dll",
          "integrity": "sha256-2d36wIN2zXiLjFhr6N0k4/9GNgx+EFe5oPsk4JWDY5E=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.n8jd7qpyqv.dll",
          "integrity": "sha256-p8HEMHrDS+ERY59a3foXKXJf53SHA/8krdVdS9o1hRs=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.ejl8yrnh3f.dll",
          "integrity": "sha256-Cn24Wa9AU3IQTcLIEzRHKOx0I8VRfLmeeidXaG/9Prs=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.kf29xlao1o.dll",
          "integrity": "sha256-kEmhU2fEYgUbTZsp10LbIbnXwW1NCYM+5aTGqgXXqs0=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.l9bfd4ew1h.dll",
          "integrity": "sha256-N3wdhGsIdHv8e7DqHCbCBu4xq0mStHnEUsBTSuN+ois=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.loi83chxrs.dll",
          "integrity": "sha256-lfNz9wVawDQsKhFnEGiAFeo6ZizBm4qmbFSEQJqkk2k=",
          "cache": "force-cache"
        }
      ],
      "ru": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.y28hi32iyz.dll",
          "integrity": "sha256-1t75dp+yZXfUnLnCCxze6XMjyH9+JXyxaW3zNPUKeOk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.32hfxcdhmo.dll",
          "integrity": "sha256-5XoLtWVoEoSivU8ClEmcBuw/qhZWbgR+qiKlZiTrbEs=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.9mo5t2764o.dll",
          "integrity": "sha256-4YDzUNazihlGQEXCsfDD5AnTA4Q+AjIbi/z2HQYW1RM=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.1pwhltjmdo.dll",
          "integrity": "sha256-O8cfqVpWTBekEeMT1kAOwL0xmS9PC1GfH2U34DFGuR0=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.u6xpw84ici.dll",
          "integrity": "sha256-RnQ89MYDOchgJ6DS7vNS0Fw1hyOyuyNISRR0rWsy9LY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.x3gy5kuv72.dll",
          "integrity": "sha256-8+TlZCYvjsWL5uL4RgJYWp1bM7nHqKcsTK4d8Hn588A=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.0de2ycv0p6.dll",
          "integrity": "sha256-NLd8I2JRYLNACYszBFmXE4/wQsqH/M+Q+b+rnesTL4o=",
          "cache": "force-cache"
        }
      ],
      "tr": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.4aeulq6oav.dll",
          "integrity": "sha256-zOAKhzLFFsMFzxN9hnvKUXZC4OgGmbqkdkMsaHt3jeo=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.vu9x5hkpq5.dll",
          "integrity": "sha256-Jznc9GitVep4kgxS5JGNynnmgUbQdguBim2JkVRL4o8=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.dohn0e7vu8.dll",
          "integrity": "sha256-wx75DzIFdVuse1McktLOeQ94lQo2D0OIViIlk+vdx4Q=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.nqvbxyhh3a.dll",
          "integrity": "sha256-b05daZTVtYNbXxsWGOU2xGv6X8/oSN9jqgPvqonfxMc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.su6mi0o6wu.dll",
          "integrity": "sha256-zOHb6hHtdfnWJ4AoDdqQxhRdb/VdW53oclWU1bwIKQE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.zv6jrelv69.dll",
          "integrity": "sha256-YVMJmEnbdFGQ2NPW5Cqol/CIG9YuMIs19Vd6huYIPNI=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.mrlcqinieo.dll",
          "integrity": "sha256-MssrAPZGCqo3KTCyWtx3Jwnp2T6pg6bKKV/1vBxrdHI=",
          "cache": "force-cache"
        }
      ],
      "zh-Hans": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.q3jy950xh2.dll",
          "integrity": "sha256-yk2cApYrNACUNXj0bbK/Ej0GK4Oo36f11FG7PT9TSWw=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.riikvlaa8x.dll",
          "integrity": "sha256-w635izHHTMl1f33JrchVbUVsCHZZeK3jADtY+PdWlHU=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.ktyctd8wsg.dll",
          "integrity": "sha256-uLPIo5FDQg3+yy9k5Ru1NhP1VPGEeiMkXo7dQCo8ybE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.svyn9400pv.dll",
          "integrity": "sha256-WEnX17A/JsomlWSpcwRaiCEyv+NdW7xHXZ3HDNq9ycE=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.7wq4q33v2d.dll",
          "integrity": "sha256-2WgVIP4wolnk8A5YCHr1p1xsXV6bmkYxEjrNFnDpGhc=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.artzjkj0mg.dll",
          "integrity": "sha256-movmpP3zPrinzdRclQhyBb55lITYUdAvTU/Vpkn5Oqk=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.vzxryhoc3g.dll",
          "integrity": "sha256-JVWIPw/u/kSRdV43/rbLA+ddJEiogIEnC18kD1Zz6NA=",
          "cache": "force-cache"
        }
      ],
      "zh-Hant": [
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Features.resources.g59j5fx4e2.dll",
          "integrity": "sha256-7HkiUuedXrz8TVKE2KYfS7qqBhYEj6AaOjjq3igKnHY=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.Workspaces.resources.iqug2oh7hd.dll",
          "integrity": "sha256-mnGnFy7gCqM2rdMZLRaACOumoGV85rqjDGJKdUB5S8Y=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.CSharp.resources.dll",
          "name": "Microsoft.CodeAnalysis.CSharp.resources.7uupalq6ag.dll",
          "integrity": "sha256-oblWPgsW2jr6G7gFFLs9kygiCLmsFNSvqaQpNpp9iaA=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Features.resources.dll",
          "name": "Microsoft.CodeAnalysis.Features.resources.v6erdo4eou.dll",
          "integrity": "sha256-l83Tiq1mwoZABD3U75QsfKbWx747OKCYznYudlVT/3A=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Scripting.resources.dll",
          "name": "Microsoft.CodeAnalysis.Scripting.resources.7ptk5a5xoh.dll",
          "integrity": "sha256-W/C2TmDrfiEGajGsX/8BiDky+6aXTqGaORSpYQZNZhw=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.Workspaces.resources.dll",
          "name": "Microsoft.CodeAnalysis.Workspaces.resources.od5qg4cwbt.dll",
          "integrity": "sha256-vP0y0iYwxiFQtjLecWxfYWhZOAmTT798aPu6xpZsctA=",
          "cache": "force-cache"
        },
        {
          "virtualPath": "Microsoft.CodeAnalysis.resources.dll",
          "name": "Microsoft.CodeAnalysis.resources.oobcinzg1t.dll",
          "integrity": "sha256-2PscfXE1Yg2dWsMwzJf6zokSzQr91Txy69eHXTU0tVk=",
          "cache": "force-cache"
        }
      ]
    }
  },
  "debugLevel": 0,
  "linkerEnabled": true,
  "globalizationMode": "sharded",
  "extensions": {
    "blazor": {}
  },
  "runtimeConfig": {
    "runtimeOptions": {
      "configProperties": {
        "Microsoft.AspNetCore.Components.Routing.RegexConstraintSupport": false,
        "Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability": true,
        "System.ComponentModel.DefaultValueAttribute.IsSupported": false,
        "System.ComponentModel.Design.IDesignerHost.IsSupported": false,
        "System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization": false,
        "System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported": false,
        "System.Data.DataSet.XmlSerializationIsSupported": false,
        "System.Diagnostics.Debugger.IsSupported": false,
        "System.Diagnostics.Metrics.Meter.IsSupported": false,
        "System.Diagnostics.Tracing.EventSource.IsSupported": false,
        "System.GC.Server": true,
        "System.Globalization.Invariant": false,
        "System.TimeZoneInfo.Invariant": false,
        "System.Linq.Enumerable.IsSizeOptimized": true,
        "System.Net.Http.EnableActivityPropagation": false,
        "System.Net.Http.WasmEnableStreamingResponse": true,
        "System.Net.SocketsHttpHandler.Http3Support": false,
        "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
        "System.Resources.ResourceManager.AllowCustomResourceTypes": false,
        "System.Resources.UseSystemResourceKeys": true,
        "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported": true,
        "System.Runtime.InteropServices.BuiltInComInterop.IsSupported": false,
        "System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting": false,
        "System.Runtime.InteropServices.EnableCppCLIHostActivation": false,
        "System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop": false,
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
        "System.StartupHookProvider.IsSupported": false,
        "System.Text.Encoding.EnableUnsafeUTF7Encoding": false,
        "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": true,
        "System.Threading.Thread.EnableAutoreleasePool": false,
        "Microsoft.AspNetCore.Components.Endpoints.NavigationManager.DisableThrowNavigationException": false
      }
    }
  }
}/*json-end*/);export{gt as default,ft as dotnet,mt as exit};
