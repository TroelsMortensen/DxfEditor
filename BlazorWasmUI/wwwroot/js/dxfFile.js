window.dxfFile = {
  /**
   * @param {Uint8Array} bytes
   * @param {string} suggestedName
   * @returns {Promise<{ok: boolean, cancelled?: boolean, error?: string}>}
   */
  async saveDxf(bytes, suggestedName) {
    const name = suggestedName && suggestedName.trim()
      ? suggestedName.trim()
      : "layout.dxf";

    // Copy immediately: Blazor WASM may pass a view into the WASM heap that is
    // invalid after subsequent awaits (e.g. showSaveFilePicker).
    const data = bytes instanceof Uint8Array
      ? bytes.slice()
      : new Uint8Array(bytes);

    if (typeof window.showSaveFilePicker === "function") {
      try {
        const handle = await window.showSaveFilePicker({
          suggestedName: name,
          types: [
            {
              description: "DXF drawing",
              accept: {
                "application/dxf": [".dxf"],
                "image/vnd.dxf": [".dxf"],
              },
            },
          ],
        });
        const writable = await handle.createWritable();
        await writable.write(data);
        await writable.close();
        return { ok: true };
      } catch (e) {
        if (e && e.name === "AbortError") {
          return { ok: false, cancelled: true };
        }
        // Fall through to download for other picker failures.
      }
    }

    try {
      downloadBlob(data, name);
      return { ok: true };
    } catch (e) {
      return {
        ok: false,
        error: e && e.message ? String(e.message) : "Failed to save DXF.",
      };
    }
  },
};

/**
 * @param {Uint8Array} data
 * @param {string} fileName
 */
function downloadBlob(data, fileName) {
  const blob = new Blob([data], { type: "application/dxf" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName.endsWith(".dxf") || fileName.endsWith(".DXF")
    ? fileName
    : `${fileName}.dxf`;
  a.style.display = "none";
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
