import { FileUp, FileType2, X, CheckCircle2, Loader2, Cpu } from "lucide-react";
import { useState, useCallback } from "react";
import { open as openDialog, message } from "@tauri-apps/plugin-dialog";
import { invoke } from "@tauri-apps/api/core";

export default function Converter() {
  const [files, setFiles] = useState<{name: string, size: number, path?: string}[]>([]);
  const [targetExt, setTargetExt] = useState<string>("");
  const [converting, setConverting] = useState(false);
  const [success, setSuccess] = useState(false);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      const newFiles = Array.from(e.dataTransfer.files).map(f => ({ name: f.name, size: f.size }));
      setFiles(prev => [...prev, ...newFiles]);
      setSuccess(false);
    }
  }, []);

  const openFileDialog = async () => {
    try {
      const selected = await openDialog({
        multiple: true,
        filters: [{
          name: 'Supported Documents',
          extensions: ['pdf', 'docx', 'xlsx', 'pptx', 'png', 'jpg', 'webp']
        }]
      });
      if (Array.isArray(selected)) {
        const newFiles = selected.map(path => {
          // Extract filename from path for mock
          const name = (path as string).split(/[\\/]/).pop() || "Unknown File";
          return { name, size: Math.floor(Math.random() * 5000000) + 100000, path: path as string };
        });
        setFiles(prev => [...prev, ...newFiles]);
        setSuccess(false);
      }
    } catch (e) {
      console.error(e);
    }
  };

  const startConversion = async () => {
    setConverting(true);
    try {
      const outDir = await invoke("get_download_dir");
      const paths = files.map(f => f.path).filter(Boolean) as string[];
      if (paths.length > 0) {
        const res = await invoke("convert_files", {
          files: paths,
          outputDir: outDir as string,
          targetExt: targetExt
        });
        await message(`${res}`, { title: 'Success', kind: 'info' });
        setSuccess(true);
        setTimeout(() => {
          setFiles([]);
          setSuccess(false);
        }, 3000);
      }
    } catch (e: any) {
      await message(`Conversion failed: ${e}`, { title: 'Error', kind: 'error' });
    }
    setConverting(false);
  };

  return (
    <div className="flex flex-col min-h-full animate-in fade-in duration-500">
      <header className="mb-8">
        <h2 className="text-3xl font-medium mb-2 text-md-on-surface flex items-center gap-3">
          <Cpu className="text-md-primary" size={28} />
          Universal Converter
        </h2>
        <p className="text-md-on-surface-variant text-lg">Convert PDFs, Office Documents, and Images locally.</p>
      </header>

      {files.length === 0 ? (
        <div 
          onDrop={handleDrop}
          onDragOver={(e) => e.preventDefault()}
          onClick={openFileDialog}
          className="flex-1 border-2 border-dashed border-md-outline-variant hover:border-md-primary/50 bg-md-surface-low/50 rounded-[2rem] flex flex-col items-center justify-center transition-all group cursor-pointer shadow-sm hover:bg-md-surface-container-low/80 p-12 min-h-[350px]"
        >
          <div className="w-24 h-24 bg-md-surface-container rounded-full flex items-center justify-center mb-8 group-hover:scale-110 transition-transform shadow-sm border border-md-outline-variant/30">
            <FileUp size={44} className="text-md-primary" />
          </div>
          <h3 className="text-2xl font-medium mb-3 text-md-on-surface">Drag and drop files</h3>
          <p className="text-md-on-surface-variant text-lg mb-8">Or click to browse your computer</p>
          <button onClick={(e) => { e.stopPropagation(); openFileDialog(); }} className="bg-md-surface-container text-md-on-surface px-8 py-3 rounded-full font-medium border border-md-outline-variant/50 hover:bg-md-surface-container-high transition-colors active:scale-95 shadow-sm">
            Browse Files
          </button>
        </div>
      ) : (
        <div className="flex-1 flex flex-col">
          <div className="flex-1 bg-md-surface-low border border-md-outline-variant/30 rounded-[2rem] p-6 shadow-sm overflow-y-auto mb-6 relative">
            
            {success && (
              <div className="absolute inset-0 z-10 bg-md-surface-low/90 backdrop-blur-sm flex flex-col items-center justify-center animate-in zoom-in-95 rounded-[2rem]">
                <CheckCircle2 size={64} className="text-green-500 mb-4" />
                <h3 className="text-2xl font-medium text-md-on-surface">Conversion Complete!</h3>
                <p className="text-md-on-surface-variant">Files saved to output directory.</p>
              </div>
            )}

            <div className="flex justify-between items-center mb-6">
              <h3 className="text-lg font-medium text-md-on-surface">Queued Files ({files.length})</h3>
              <button onClick={() => setFiles([])} className="text-sm text-red-500 hover:text-red-600 flex items-center gap-1"><X size={16}/> Clear All</button>
            </div>
            <div className="space-y-3">
              {files.map((f, i) => (
                <div key={i} className="flex justify-between items-center p-4 rounded-xl border border-md-outline-variant/30 bg-md-surface-container shadow-sm">
                  <span className="text-md-on-surface font-medium truncate max-w-[70%]">{f.name}</span>
                  <span className="text-md-on-surface-variant text-sm">{(f.size / 1024 / 1024).toFixed(2)} MB</span>
                </div>
              ))}
            </div>
          </div>
          
          <div className="bg-md-surface-container border border-md-outline-variant/30 p-6 rounded-[2rem] flex items-end justify-between shadow-sm">
            <div className="flex-1 mr-8">
              <label className="block text-sm font-medium text-md-on-surface-variant mb-2">Target Format</label>
              <select value={targetExt} onChange={e => setTargetExt(e.target.value)} className="bg-md-surface-low border border-md-outline-variant/50 text-md-on-surface text-sm rounded-xl focus:ring-md-primary focus:border-md-primary block w-full p-2.5 outline-none">
                <option value="">Select Extension</option>
                <option value=".pdf" disabled={files.length > 0 && files.every(f => f.name.toLowerCase().endsWith('.pdf'))}>PDF Document (.pdf)</option>
                <option value=".docx" disabled={files.length > 0 && files.every(f => f.name.toLowerCase().endsWith('.docx'))}>Word Document (.docx)</option>
                <option value=".png" disabled={files.length > 0 && files.every(f => f.name.toLowerCase().endsWith('.png'))}>PNG Image (.png)</option>
                <option value=".webp" disabled={files.length > 0 && files.every(f => f.name.toLowerCase().endsWith('.webp'))}>WebP Image (.webp)</option>
              </select>
            </div>
            <button onClick={startConversion} disabled={!targetExt || converting || files.length === 0} className="flex items-center gap-2 bg-md-primary text-md-on-primary px-8 py-3 rounded-full font-medium hover:brightness-110 transition-all shadow-md active:scale-95 disabled:opacity-50">
              {converting && <Loader2 size={18} className="animate-spin" />}
              {converting ? 'Processing...' : 'Start Conversion'}
            </button>
          </div>
        </div>
      )}

      <div className="mt-6 bg-md-surface-low rounded-[2rem] p-6 border border-md-outline-variant/30 shadow-sm">
        <h4 className="font-medium mb-4 flex items-center gap-2 text-md-on-surface">
          <FileType2 size={20} className="text-md-primary" />
          Supported Formats
        </h4>
        <div className="flex gap-3 flex-wrap">
          {['.PDF', '.DOCX', '.XLSX', '.PPTX', '.PNG', '.JPG', '.WEBP'].map(ext => (
            <span key={ext} className="bg-md-surface-container px-4 py-2 rounded-xl text-sm font-medium text-md-on-surface-variant border border-md-outline-variant/30 shadow-sm hover:scale-105 transition-transform cursor-default">
              {ext}
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}
