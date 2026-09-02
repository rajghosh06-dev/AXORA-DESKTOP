import { Layers, ImagePlus, Settings2, X, SlidersHorizontal, Save, Loader2 } from "lucide-react";
import { useState, useEffect } from "react";
import { open as openDialog, message } from "@tauri-apps/plugin-dialog";
import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";

export default function BatchProcessor() {
  const [showSettings, setShowSettings] = useState(false);
  const [maxSizeValue, setMaxSizeValue] = useState("500");
  const [maxSizeUnit, setMaxSizeUnit] = useState("KB");
  const [targetExt, setTargetExt] = useState(".webp");
  const [items, setItems] = useState<string[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [progress, setProgress] = useState(0);
  const [total, setTotal] = useState(0);

  useEffect(() => {
    let unlisten: () => void;
    
    listen<{ processed: number, total: number }>("batch-progress", (event) => {
      setProgress(event.payload.processed);
      setTotal(event.payload.total);
    }).then(f => unlisten = f);

    return () => {
      if (unlisten) unlisten();
    };
  }, []);

  const handleAddFolder = async () => {
    try {
      const selected = await openDialog({
        directory: true,
        multiple: true,
        title: 'Select Folders to Process'
      });
      if (Array.isArray(selected)) {
        setItems(prev => [...prev, ...selected.map(s => s as string)]);
      } else if (selected) {
        setItems(prev => [...prev, selected as string]);
      }
    } catch(e) {
      console.error(e);
    }
  };

  const handleProcess = async () => {
    if (items.length === 0) return;
    setIsProcessing(true);
    setProgress(0);
    setTotal(items.length);
    try {
      const outDir = await invoke("get_download_dir");
      const res = await invoke("batch_process_images", {
        files: items,
        outputDir: outDir as string,
        maxSizeValue: Number(maxSizeValue) || 500,
        maxSizeUnit: maxSizeUnit,
        targetExt: targetExt
      });
      await message(`${res}`, { title: 'Batch Processing Complete', kind: 'info' });
      setItems([]);
    } catch (e: any) {
      await message(`Processing failed: ${e}`, { title: 'Error', kind: 'error' });
    }
    setIsProcessing(false);
    setProgress(0);
    setTotal(0);
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500 relative">
      <header className="mb-8 flex justify-between items-end">
        <div>
          <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
            <Layers className="text-md-primary" size={28} />
            Batch Image Processor
          </h2>
          <p className="text-md-on-surface-variant text-lg">Process up to 3000 images simultaneously.</p>
        </div>
        <button 
          onClick={() => setShowSettings(!showSettings)}
          className={`p-3 rounded-full transition-colors active:scale-95 shadow-sm ${showSettings ? 'bg-md-primary text-md-on-primary shadow-md' : 'bg-md-surface-container text-md-on-surface hover:bg-md-surface-container-high'}`}
        >
          <Settings2 size={24} />
        </button>
      </header>

      <div className="flex-1 bg-md-surface-low/30 border border-md-outline-variant/30 rounded-[2rem] flex flex-col overflow-hidden shadow-sm backdrop-blur-sm relative">
        <div className="p-6 border-b border-md-outline-variant/30 flex items-center justify-between bg-transparent">
          <h3 className="font-medium flex items-center gap-2 text-md-on-surface">
            <Layers size={20} className="text-md-primary" />
            Processing Queue
          </h3>
          <span className="bg-md-surface-container px-4 py-1.5 rounded-full text-sm font-medium text-md-on-surface-variant">
            {items.length} Items
          </span>
        </div>

        {/* Settings Inline Accordion */}
        {showSettings && (
          <div className="bg-md-surface-container/50 border-b border-md-outline-variant/30 p-6 shadow-inner animate-in slide-in-from-top-2 fade-in duration-200">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-medium flex items-center gap-2 text-md-on-surface">
                <SlidersHorizontal size={18} className="text-md-primary"/>
                ImageMagick Parameters
              </h3>
              <button onClick={() => setShowSettings(false)} className="p-1.5 rounded-full hover:bg-md-surface-container-high text-md-on-surface-variant transition-colors"><X size={16}/></button>
            </div>
            <div className="grid grid-cols-1 lg:grid-cols-3 md:grid-cols-2 gap-4 lg:gap-6">
              <div>
                <label className="block text-sm font-medium text-md-on-surface-variant mb-2">Max File Size</label>
                <div className="flex bg-md-surface-low border border-md-outline-variant/30 rounded-xl overflow-hidden shadow-sm focus-within:border-md-primary focus-within:ring-1 focus-within:ring-md-primary">
                  <input type="number" value={maxSizeValue} onChange={e => setMaxSizeValue(e.target.value)} className="w-full bg-transparent p-2.5 outline-none text-md-on-surface text-sm" min="1" placeholder="Enter size..." />
                  <select value={maxSizeUnit} onChange={e => setMaxSizeUnit(e.target.value)} className="bg-md-surface-container-high border-l border-md-outline-variant/30 text-md-on-surface text-sm p-2.5 outline-none font-medium cursor-pointer appearance-none px-4">
                    <option value="KB">KB</option>
                    <option value="MB">MB</option>
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-md-on-surface-variant mb-2">Target Extension</label>
                <select value={targetExt} onChange={e => setTargetExt(e.target.value)} className="bg-md-surface-low border border-md-outline-variant/30 text-md-on-surface text-sm rounded-xl focus:ring-md-primary focus:border-md-primary block w-full p-2.5 outline-none shadow-sm">
                  <option value=".webp">Optimized WebP (.webp)</option>
                  <option value=".jpg">Standard JPEG (.jpg)</option>
                  <option value=".png">Lossless PNG (.png)</option>
                </select>
              </div>
              <div className="flex items-end md:col-span-2 lg:col-span-1">
                 <button onClick={() => setShowSettings(false)} className="w-full whitespace-nowrap bg-md-primary text-md-on-primary px-4 py-2.5 rounded-full font-medium hover:brightness-110 transition-all flex items-center justify-center gap-2 shadow-md active:scale-95">
                   <Save size={18} className="flex-shrink-0" />
                   Save Configuration
                 </button>
              </div>
            </div>
          </div>
        )}
        
        {items.length === 0 ? (
          <div onClick={handleAddFolder} className="flex-1 flex flex-col items-center justify-center p-12 min-h-[350px] text-center border-2 border-dashed border-md-outline-variant/50 m-6 rounded-[2rem] cursor-pointer hover:border-md-primary/50 hover:bg-md-surface-container-low/30 transition-all group">
            <div className="w-24 h-24 rounded-full bg-md-surface-container flex items-center justify-center mb-8 text-md-on-surface-variant shadow-sm group-hover:scale-110 transition-transform border border-md-outline-variant/30">
              <ImagePlus size={44} />
            </div>
            <h4 className="text-2xl font-medium mb-3 text-md-on-surface">Add Folder or Images</h4>
            <p className="text-md-on-surface-variant text-lg">Resize, compress, watermark, and convert formats</p>
          </div>
        ) : (
          <div className="flex-1 flex flex-col m-6">
            <div className="flex-1 overflow-y-auto mb-6 bg-md-surface-container rounded-2xl border border-md-outline-variant/30 p-4 space-y-2">
              {items.map((item, idx) => (
                <div key={idx} className="p-3 bg-md-surface-low border border-md-outline-variant/30 rounded-xl text-sm font-medium text-md-on-surface-variant shadow-sm truncate">
                  {item}
                </div>
              ))}
            </div>
            <div className="flex gap-4">
                <button onClick={() => setItems([])} disabled={isProcessing} className="px-6 py-3 rounded-full font-medium text-red-500 bg-red-500/10 hover:bg-red-500/20 transition-colors shadow-sm disabled:opacity-50">
                 Clear Queue
               </button>
               <button onClick={handleProcess} disabled={isProcessing || !maxSizeValue || Number(maxSizeValue) <= 0} className="flex-1 flex items-center justify-center gap-2 bg-md-primary text-md-on-primary px-8 py-3 rounded-full font-medium hover:brightness-110 transition-all md-elevation-1 active:scale-95 disabled:opacity-70">
                 {isProcessing && <Loader2 size={20} className="animate-spin" />}
                 {isProcessing ? (total > 0 ? `Processing ${progress} / ${total}` : 'Initializing...') : `Process ${items.length} Images`}
               </button>
            </div>
            {isProcessing && total > 0 && (
              <div className="mt-4 animate-in fade-in zoom-in duration-300">
                <div className="flex justify-between text-sm mb-2 text-md-on-surface-variant font-medium">
                  <span>Batch Progress</span>
                  <span>{Math.round((progress / total) * 100)}%</span>
                </div>
                <div className="w-full bg-md-surface-low rounded-full h-2 shadow-inner">
                  <div className="bg-md-primary h-2 rounded-full transition-all duration-200 ease-out shadow-sm" style={{ width: `${(progress / total) * 100}%` }}></div>
                </div>
              </div>
            )}
          </div>
        )}


      </div>
    </div>
  );
}
