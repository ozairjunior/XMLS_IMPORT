window.xmlFiscal = {
  downloadFileFromStream: async function (fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || 'exportacao';
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }
};
