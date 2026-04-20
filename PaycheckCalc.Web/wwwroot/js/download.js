// Helper invoked from Blazor via IJSRuntime to trigger a browser file download.
// Usage: await JS.InvokeVoidAsync("downloadFile", fileName, mimeType, byteArray);
window.downloadFile = function (fileName, mimeType, content) {
    const blob = new Blob([new Uint8Array(content)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
