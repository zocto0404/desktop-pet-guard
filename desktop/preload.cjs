const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('desktopPet',{ready:()=>ipcRenderer.send('pet:ready'),menu:()=>ipcRenderer.send('pet:menu'),hitTest:hit=>ipcRenderer.send('pet:hit',!!hit),move:(dx,dy)=>ipcRenderer.send('pet:move',{dx,dy}),onCommand:fn=>ipcRenderer.on('pet:command',(_,command)=>fn(command))});
