window.downloadFile = function (filename, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

window.appGetTheme = function () {
    return localStorage.getItem('pts-theme') || 'light';
};

window.appSetTheme = function (theme) {
    localStorage.setItem('pts-theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
};
