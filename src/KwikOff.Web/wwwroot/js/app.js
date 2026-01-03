// KwikOff Application JavaScript

// Simple file download for small files
window.downloadFile = function (fileName, base64Content) {
    const link = document.createElement('a');
    link.href = 'data:text/csv;base64,' + base64Content;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Chunked download for large files using Blob API
let largeDownloadChunks = [];
let largeDownloadFileName = '';

window.initLargeDownload = function (fileName) {
    largeDownloadChunks = [];
    largeDownloadFileName = fileName;
    console.log('Initialized large download for:', fileName);
};

window.appendChunk = function (base64Chunk) {
    // Convert base64 to binary immediately to save memory
    const binaryString = atob(base64Chunk);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    largeDownloadChunks.push(bytes);
    console.log('Appended chunk', largeDownloadChunks.length);
};

window.finalizeDownload = function () {
    console.log('Finalizing download with', largeDownloadChunks.length, 'chunks');
    
    // Create a Blob from all chunks (much more efficient than data URL)
    const blob = new Blob(largeDownloadChunks, { type: 'text/csv' });
    
    // Create object URL (no size limit)
    const url = URL.createObjectURL(blob);
    
    // Create download link
    const link = document.createElement('a');
    link.href = url;
    link.download = largeDownloadFileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    
    // Clean up
    setTimeout(() => URL.revokeObjectURL(url), 100);
    largeDownloadChunks = [];
    largeDownloadFileName = '';
    
    console.log('Download complete');
};

