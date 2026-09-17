# Third-party components

- Three.js r186, MIT. Vendored from https://github.com/mrdoob/three.js/tree/r186 . License: viewer/vendor/three/LICENSE.
- Meshoptimizer decoder, MIT. Existing decoder from https://github.com/zeux/meshoptimizer ; license retained in tools.
- Microsoft WebView2 SDK 1.0.1938.49: redistributable components from the official NuGet package. SDK license and notices: viewer/vendor/webview2/LICENSE.txt and NOTICE.txt. https://learn.microsoft.com/microsoft-edge/webview2/ . The Evergreen WebView2 Runtime is required.
- RhinoCommon: requires a licensed Rhino 8 installation. Compilation uses the official RhinoCommon 8.0.23304.9001 NuGet reference; Rhino SDK assemblies are not redistributed.

The preview loads local scripts and embedded GLB assets only. No API keys are passed to WebView2. Cache and user data are under the selected data directory in work/.

- Phosphor Icons, MIT, official vectors from https://github.com/phosphor-icons/core . License: viewer/vendor/icons/LICENSE.

- Remy logo, cat motion reference, and pixel-block loading movie: supplied by the user for this project. These are separate from the third-party code licenses above.

## Local size inference (0.4.1)

- CLIP ViT-B/32, MIT, OpenAI. ONNX quantization from Xenova/clip-vit-base-patch32, pinned revision d15189d7028b43f1d3e65039190477f6af591c2a. License: runtime/vision/CLIP-LICENSE.txt. Model/config hashes: runtime/vision/manifest.json. https://huggingface.co/Xenova/clip-vit-base-patch32
- Transformers.js 3.8.1, Apache-2.0, Hugging Face. Source, license and dependency notices retained under runtime/vision/node_modules. https://github.com/huggingface/transformers.js
- ONNX Runtime, MIT, Microsoft; Sharp and other transitive dependencies retain their licenses in node_modules. Exact versions/integrities are fixed by runtime/vision/package-lock.json.
- Node.js: bundled runtime under runtime/node.exe; see https://github.com/nodejs/node/blob/main/LICENSE .

Inference loads only local model weights with allowRemoteModels=false. A private image snapshot under the selected data directory in work/ is deleted on completion. No Tripo key is passed to this subprocess. CLIP visual labels plus product size heuristics provide estimates, not camera-based measurements or exact physical scale.

## Framework compatibility (0.5.2)

System.Text.Json 8.0.6 and its Microsoft dependencies are distributed under their NuGet licenses (MIT). Exact resolved versions are recorded in installer/runtime-manifests/dotnet-0.5.2.json; license texts are retained in viewer/vendor/dotnet. PolySharp 1.16.0 (MIT) generates compile-time compatibility types and is not a runtime service. No Rhino runtime setting is modified.
