import * as XLSX from 'xlsx';

const HEADERS = ['序号', '随机码', '固定码', '考题类型', '题目', '选项A', '选项B', '选项C', '选项D', '选项E', '答案', '来源', '一级', '二级', '三级', '四级', '五级', '六级'];

function questionRow(overrides: Record<string, unknown>): unknown[] {
  const row: Record<string, unknown> = {
    序号: 1,
    考题类型: '单选题',
    题目: '基础题目',
    选项A: '答案一',
    选项B: '答案二',
    答案: 'A',
    来源: '2-基站（普朗克）功能简介',
    一级: '√',
    ...overrides,
  };
  return HEADERS.map((header) => row[header] ?? '');
}

export function createWorkbookFixture(): XLSX.WorkBook {
  const workbook = XLSX.utils.book_new();
  const rows = [
    ['题库说明'],
    [],
    [],
    HEADERS,
    questionRow({}),
    questionRow({ 序号: 2, 考题类型: '判断题', 题目: '<img src=x onerror=alert(1)>', 选项A: '√', 选项B: '×', 答案: 'B', 来源: '地宝功能简介.pptx', 一级: '', 二级: '√' }),
    questionRow({ 序号: 3, 考题类型: '多选题', 题目: '多选题目', 选项C: '答案三', 答案: 'CA', 来源: '5-光学组件功能介绍', 一级: '', 三级: '√' }),
    questionRow({ 序号: 4, 考题类型: '简答题', 题目: '请简述流程', 选项A: '', 选项B: '', 答案: '标准答案', 来源: '3-窗宝功能简介', 一级: '', 四级: '√' }),
  ];
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet(rows), '正常题库');
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet(rows), '数据透视表');
  return workbook;
}
