interface SheetSelectionModalProps {
  sheetNames: string[];
  selectedSheets: string[];
  onChange: (sheets: string[]) => void;
  onCancel: () => void;
  onConfirm: () => void;
}

export function SheetSelectionModal({
  sheetNames,
  selectedSheets,
  onChange,
  onCancel,
  onConfirm,
}: SheetSelectionModalProps) {
  if (sheetNames.length === 0) return null;

  const toggleSheet = (sheetName: string) => {
    onChange(
      selectedSheets.includes(sheetName)
        ? selectedSheets.filter((name) => name !== sheetName)
        : [...selectedSheets, sheetName],
    );
  };

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="sheet-title">
        <h2 id="sheet-title">选择要加载的题库（Sheet）</h2>
        <p className="muted">名称包含“透视”的 Sheet 默认不选中，仍可手动选择。</p>
        <div className="sheet-list">
          {sheetNames.map((sheetName) => (
            <label className="check-chip sheet-chip" key={sheetName}>
              <input
                type="checkbox"
                checked={selectedSheets.includes(sheetName)}
                onChange={() => toggleSheet(sheetName)}
              />
              <span>{sheetName}</span>
            </label>
          ))}
        </div>
        <div className="modal-actions">
          <button className="button button-ghost" type="button" onClick={onCancel}>取消</button>
          <button className="button button-primary" type="button" onClick={onConfirm}>确定加载</button>
        </div>
      </section>
    </div>
  );
}
