import fs from 'node:fs';
import { MeshoptDecoder } from './meshopt_decoder.mjs';
// Local-only conversion: embedded buffers stay on this computer.
const bytes = fs.readFileSync(process.argv[2]);
if (bytes.length > 150 * 1024 * 1024 || bytes.readUInt32LE(0) !== 0x46546c67 || bytes.readUInt32LE(4) !== 2 || bytes.readUInt32LE(8) !== bytes.length) throw Error('Invalid GLB');
let root, bin;
for (let p = 12; p < bytes.length;) {
  const length = bytes.readUInt32LE(p), type = bytes.readUInt32LE(p + 4); p += 8;
  if (p + length > bytes.length) throw Error('Invalid chunk');
  if (type === 0x4e4f534a) root = JSON.parse(bytes.subarray(p, p + length).toString());
  if (type === 0x004e4942) bin = bytes.subarray(p, p + length);
  p += length;
}
if (!root || !bin) throw Error('Missing GLB chunks');
await MeshoptDecoder.ready;
const chunks = []; let offset = 0;
for (const view of root.bufferViews) {
  const ext = view.extensions?.EXT_meshopt_compression;
  const source = ext || view;
  if ((source.buffer || 0) !== 0) throw Error('External buffers unsupported');
  const start = source.byteOffset || 0, length = source.byteLength;
  if (!Number.isSafeInteger(start) || !Number.isSafeInteger(length) || start < 0 || length < 0 || start + length > bin.length) throw Error('Invalid buffer range');
  let output;
  if (ext) {
    const size = ext.count * ext.byteStride;
    if (!Number.isSafeInteger(size) || size < 0 || size > 150 * 1024 * 1024 || size !== view.byteLength) throw Error('Invalid decoded size');
    output = Buffer.alloc(size);
    MeshoptDecoder.decodeGltfBuffer(output, ext.count, ext.byteStride, bin.subarray(start, start + length), ext.mode, ext.filter);
    delete view.extensions.EXT_meshopt_compression;
  } else output = bin.subarray(start, start + length);
  view.buffer = 0; view.byteOffset = offset; view.byteLength = output.length;
  chunks.push(output); offset += output.length;
  const padding = (4 - offset % 4) % 4; chunks.push(Buffer.alloc(padding)); offset += padding;
  if (offset > 150 * 1024 * 1024) throw Error('Decoded GLB too large');
}
root.buffers = [{ byteLength: offset }];
for (const key of ['extensionsRequired', 'extensionsUsed']) if (root[key]) root[key] = root[key].filter(x => x !== 'EXT_meshopt_compression');
let json = Buffer.from(JSON.stringify(root)); json = Buffer.concat([json, Buffer.alloc((4 - json.length % 4) % 4, 32)]);
const header = Buffer.alloc(20); header.writeUInt32LE(0x46546c67); header.writeUInt32LE(2,4); header.writeUInt32LE(28 + json.length + offset,8); header.writeUInt32LE(json.length,12); header.writeUInt32LE(0x4e4f534a,16);
const bh = Buffer.alloc(8); bh.writeUInt32LE(offset); bh.writeUInt32LE(0x004e4942,4);
fs.writeFileSync(process.argv[3], Buffer.concat([header, json, bh, ...chunks]));
