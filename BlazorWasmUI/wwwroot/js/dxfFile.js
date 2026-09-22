window.dxfFile = {
  /**
   * Always uses a browser download. File System Access (showSaveFilePicker) is
   * skipped: in Cursor's embedded browser it can truncate a file then fall through
   * to a second download, leaving an empty .dxf behind.
   *
   * @param {Uint8Array} bytes
   * @param {string} suggestedName
   * @returns {Promise<{ok: boolean, cancelled?: boolean, error?: string}>}
   */
  async saveDxf(bytes, suggestedName) {
    const name = suggestedName && suggestedName.trim()
      ? suggestedName.trim()
      : "layout.dxf";

    // Copy immediately: Blazor WASM may pass a view into the WASM heap that is
    // invalid after subsequent awaits.
    const data = bytes instanceof Uint8Array
      ? bytes.slice()
      : new Uint8Array(bytes);

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
