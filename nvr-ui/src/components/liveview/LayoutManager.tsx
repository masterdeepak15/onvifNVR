import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { layoutService } from '@/lib/services';
import type { GridLayout, GridPosition } from '@/types/nvr';
import { Save, FolderOpen, Plus, Trash2, X } from 'lucide-react';
import { toast } from 'sonner';

interface LayoutManagerProps {
  layoutName: string;
  onLayoutNameChange: (name: string) => void;
  currentPositions: GridPosition[];
  gridColumns: number;
  onLayoutLoad: (layout: GridLayout) => void;
}

export function LayoutManager({
  layoutName, onLayoutNameChange, currentPositions, gridColumns, onLayoutLoad
}: LayoutManagerProps) {
  const qc = useQueryClient();
  const [showSaveAs, setShowSaveAs] = useState(false);
  const [newLayoutName, setNewLayoutName] = useState('');

  const { data: layoutNames = [] } = useQuery({
    queryKey: ['layout-names'],
    queryFn: () => layoutService.getNames(),
  });

  const loadLayout = async (name: string) => {
    try {
      const layout = await layoutService.get(name);
      onLayoutNameChange(name);
      onLayoutLoad(layout);
      toast.success(`Layout "${name}" loaded`);
    } catch {
      toast.error('Failed to load layout');
    }
  };

  const saveMutation = useMutation({
    mutationFn: (layout: GridLayout) => layoutService.save(layout),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['layout-names'] });
      toast.success('Layout saved');
      setShowSaveAs(false);
    },
    onError: () => toast.error('Failed to save layout'),
  });

  const saveCurrentLayout = (name?: string) => {
    const saveName = name || layoutName || 'Default';
    saveMutation.mutate({
      layoutName: saveName,
      gridColumns,
      positions: currentPositions,
    });
    if (name) onLayoutNameChange(name);
  };

  return (
    <div className="flex items-center gap-2">
      {/* Layout selector */}
      <select
        value={layoutName}
        onChange={e => loadLayout(e.target.value)}
        className="h-8 px-3 bg-muted border-none rounded-md text-sm focus:ring-1 focus:ring-primary"
      >
        {layoutNames.map(n => <option key={n} value={n}>{n}</option>)}
        {layoutNames.length === 0 && <option value="Default">Default</option>}
      </select>

      {/* Save */}
      <button onClick={() => saveCurrentLayout()} title="Save Layout"
        className="p-2 rounded-md text-muted-foreground hover:bg-muted hover:text-foreground transition-colors">
        <Save className="w-4 h-4" />
      </button>

      {/* Save As */}
      <div className="relative">
        <button onClick={() => setShowSaveAs(!showSaveAs)} title="Save As..."
          className="p-2 rounded-md text-muted-foreground hover:bg-muted hover:text-foreground transition-colors">
          <Plus className="w-4 h-4" />
        </button>
        {showSaveAs && (
          <div className="absolute top-full mt-1 right-0 bg-card border rounded-lg shadow-xl z-50 p-3 w-56">
            <p className="text-xs text-muted-foreground mb-2">Save layout as:</p>
            <input
              value={newLayoutName}
              onChange={e => setNewLayoutName(e.target.value)}
              placeholder="Layout name"
              className="h-8 w-full px-3 bg-muted border-none rounded-md text-sm mb-2 focus:ring-1 focus:ring-primary"
              autoFocus
              onKeyDown={e => { if (e.key === 'Enter' && newLayoutName.trim()) saveCurrentLayout(newLayoutName.trim()); }}
            />
            <div className="flex gap-2">
              <button onClick={() => { if (newLayoutName.trim()) saveCurrentLayout(newLayoutName.trim()); }}
                disabled={!newLayoutName.trim()}
                className="h-7 px-3 bg-primary text-primary-foreground rounded text-xs disabled:opacity-50">
                Save
              </button>
              <button onClick={() => setShowSaveAs(false)}
                className="h-7 px-3 bg-muted rounded text-xs">
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
