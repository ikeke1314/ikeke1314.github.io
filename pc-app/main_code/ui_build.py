import sys
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, 
                             QLabel, QPushButton, QStackedWidget, QRadioButton, QCheckBox, 
                             QButtonGroup, QScrollArea, QMessageBox, QComboBox, QGridLayout, QFrame,
                             QFileDialog, QDialog, QListWidget, QListWidgetItem, QDialogButtonBox)
from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtGui import QFont, QColor, QPalette
import json
import os
import subprocess
import threading

class SheetSelectionDialog(QDialog):
    def __init__(self, sheet_names, parent=None):
        super().__init__(parent)
        self.setWindowTitle("选择题库Sheet")
        self.resize(400, 300)
        layout = QVBoxLayout(self)
        
        self.list_widget = QListWidget()
        for name in sheet_names:
            item = QListWidgetItem(name)
            item.setCheckState(Qt.CheckState.Checked)
            self.list_widget.addItem(item)
        layout.addWidget(self.list_widget)
        
        btns = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        btns.accepted.connect(self.accept)
        btns.rejected.connect(self.reject)
        layout.addWidget(btns)
        
    def get_selected_sheets(self):
        selected = []
        for i in range(self.list_widget.count()):
            item = self.list_widget.item(i)
            if item.checkState() == Qt.CheckState.Checked:
                selected.append(item.text())
        return selected



class MainWindow(QMainWindow):
    def __init__(self, exam_manager):
        super().__init__()
        self.manager = exam_manager
        self.setWindowTitle("模拟考试系统 v2.3 - ©️生产技术部PE组")
        self.resize(1000, 700)
        self.setStyleSheet("""
            QMainWindow { background-color: #f0f2f5; }
            QPushButton { 
                background-color: #007bff; color: white; border-radius: 5px; padding: 10px; font-size: 14px; 
            }
            QPushButton:hover { background-color: #0056b3; }
            QLabel { font-family: 'Microsoft YaHei'; }
            QRadioButton { font-size: 14px; padding: 5px; }
            QCheckBox { font-size: 14px; padding: 5px; }
        """)

        self.central_widget = QStackedWidget()
        self.setCentralWidget(self.central_widget)

        self.init_home_page()
        self.init_exam_page()
        self.init_result_page()
        self.init_practice_menu()
        
        self.is_practice_mode = False
        self.is_review_mode = False

        self.central_widget.setCurrentWidget(self.home_page)
        
        # Practice Progress
        self.practice_file = "practice_progress.json"
        self.practice_data = self.load_practice_progress()
        
        # Auto-load bank if config exists
        self.load_config_and_bank()

        # Auto-load bank if config exists
        self.load_config_and_bank()

        # TTS Process holder
        self.tts_process = None
        
        # Auto-read timer (延迟500ms自动读题)
        self.auto_read_timer = QTimer(self)
        self.auto_read_timer.setSingleShot(True)
        self.auto_read_timer.timeout.connect(self.read_current_question)
        
        # Auto-next timer (答对后自动跳转下一题)
        self.auto_next_timer = QTimer(self)
        self.auto_next_timer.setSingleShot(True)
        self.auto_next_timer.timeout.connect(lambda: self.load_question(self.current_index + 1) if self.current_index < len(self.questions) - 1 else None)

    def load_config_and_bank(self):
        config_path = "config.json"
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    config = json.load(f)
                    last_bank = config.get('last_bank_path')
                    if last_bank and os.path.exists(last_bank):
                        self.load_bank_file(last_bank, silent=True)
            except Exception as e:
                print(f"Error loading config: {e}")

    def sort_levels(self, levels):
        """Sort levels with Chinese numerals handling."""
        chinese_nums = {'一': 1, '二': 2, '三': 3, '四': 4, '五': 5, '六': 6, '七': 7, '八': 8, '九': 9, '十': 10}
        
        def get_level_value(lvl_str):
            # Extract number from string like "一级"
            for char in lvl_str:
                if char in chinese_nums:
                    return chinese_nums[char]
            return 999 # Fallback
            
        return sorted(levels, key=get_level_value)

    def init_home_page(self):
        self.home_page = QWidget()
        layout = QVBoxLayout()
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.setSpacing(20)

        title = QLabel("技能士理论考核模拟系统 v2.1")
        title.setFont(QFont("Microsoft YaHei", 24, QFont.Weight.Bold))
        title.setAlignment(Qt.AlignmentFlag.AlignCenter)
        
        # Load Button (Moved to top)
        btn_load = QPushButton("加载自定义题库")
        btn_load.setFixedWidth(200)
        btn_load.setStyleSheet("background-color: #6f42c1; color: white; border-radius: 5px; padding: 10px; font-size: 14px;")
        btn_load.clicked.connect(self.load_custom_bank)
        
        self.level_combo = QComboBox()
        self.level_combo.currentTextChanged.connect(self.update_rule_description)
        
        if self.manager.bank:
            self.level_combo.addItems(self.sort_levels(list(self.manager.bank.levels)))
        else:
            self.level_combo.addItem("请先加载题库")
            self.level_combo.setEnabled(False)
        self.level_combo.setFixedWidth(200)
        self.level_combo.setStyleSheet("padding: 8px; font-size: 14px;")
        
        # Rule Description Label
        self.lbl_rule_desc = QLabel("")
        self.lbl_rule_desc.setFont(QFont("Microsoft YaHei", 9))  # 减小字体大小以适应单行
        self.lbl_rule_desc.setStyleSheet("color: #666; margin-top: 5px;")
        self.lbl_rule_desc.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.lbl_rule_desc.setWordWrap(False)
        
        # Category filter (exclusive selection)
        self.category_combo = QComboBox()
        self.category_combo.addItems(["全部", "基站", "地宝", "窗宝", "光学组件"])
        self.category_combo.setFixedWidth(200)
        self.category_combo.setStyleSheet("padding: 8px; font-size: 14px;")

        self.btn_mock_exam = QPushButton("开始模拟考试")
        self.btn_mock_exam.setFixedWidth(200)
        self.btn_mock_exam.clicked.connect(self.start_mock_exam)
        if not self.manager.bank:
            self.btn_mock_exam.setEnabled(False)

        self.btn_practice = QPushButton("专项练习模式")
        self.btn_practice.setFixedWidth(200)
        self.btn_practice.clicked.connect(self.open_practice_menu)
        if not self.manager.bank:
            self.btn_practice.setEnabled(False)

        btn_error_book = QPushButton("我的错题本")
        btn_error_book.setFixedWidth(200)
        btn_error_book.clicked.connect(self.start_error_review)

        # Add widgets to layout
        layout.addWidget(title)
        layout.addSpacing(10)
        
        # Load button centered
        layout.addWidget(btn_load, 0, Qt.AlignmentFlag.AlignCenter)
        layout.addSpacing(10)
        
        # Level and Source selection in horizontal layout
        selection_layout = QHBoxLayout()
        selection_layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        selection_layout.setSpacing(40)
        
        # Level selection
        level_layout = QVBoxLayout()
        level_layout.setSpacing(8)
        lbl_select = QLabel("请选择等级:")
        lbl_select.setFont(QFont("Microsoft YaHei", 13))
        lbl_select.setAlignment(Qt.AlignmentFlag.AlignCenter)
        level_layout.addWidget(lbl_select)
        level_layout.addWidget(self.level_combo)
        level_layout.addWidget(self.lbl_rule_desc)
        
        # Source selection
        source_layout = QVBoxLayout()
        source_layout.setSpacing(8)
        lbl_source = QLabel("请选择类别:")
        lbl_source.setFont(QFont("Microsoft YaHei", 13))
        lbl_source.setAlignment(Qt.AlignmentFlag.AlignCenter)
        source_layout.addWidget(lbl_source)
        source_layout.addWidget(self.category_combo)
        
        # Add placeholder label to align with rule description on the left
        lbl_category_placeholder = QLabel("")
        lbl_category_placeholder.setFont(QFont("Microsoft YaHei", 10))
        lbl_category_placeholder.setStyleSheet("color: #666; margin-top: 5px;")
        lbl_category_placeholder.setAlignment(Qt.AlignmentFlag.AlignCenter)
        lbl_category_placeholder.setMinimumHeight(20)  # 设置最小高度保持对齐
        lbl_category_placeholder.setWordWrap(False)  # 禁用换行
        source_layout.addWidget(lbl_category_placeholder)
        
        selection_layout.addLayout(level_layout)
        selection_layout.addLayout(source_layout)
        
        layout.addLayout(selection_layout)
        layout.addSpacing(20)
        
        # Main Buttons (Centered Vertical Layout)
        buttons_layout = QVBoxLayout()
        buttons_layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        buttons_layout.setSpacing(15)
        
        buttons_layout.addWidget(self.btn_mock_exam)
        buttons_layout.addWidget(self.btn_practice)
        buttons_layout.addWidget(btn_error_book)
        
        layout.addLayout(buttons_layout)

        self.home_page.setLayout(layout)
        self.central_widget.addWidget(self.home_page)
        
        # Initial update of rule description
        self.update_rule_description(self.level_combo.currentText())

    def update_rule_description(self, level_text):
        # 确保lbl_rule_desc已初始化
        if not hasattr(self, 'lbl_rule_desc'):
            return
            
        if not level_text or level_text == "请先加载题库":
            self.lbl_rule_desc.setText("")
            return
            
        weights = self.manager.LEVEL_WEIGHTS.get(level_text)
        if weights:
            desc_parts = []
            for src_lvl, weight in weights.items():
                percentage = int(weight * 100)
                desc_parts.append(f"{percentage}% {src_lvl}")
            
            desc = "出题规则: " + "、".join(desc_parts) + "题目"
            self.lbl_rule_desc.setText(desc)
        else:
            self.lbl_rule_desc.setText("出题规则: 100% 本等级题目")
    
    def get_selected_sources(self):
        """
        Get filtered sources based on category selection.
        Returns list of sources to include, or None for all sources.
        """
        category = self.category_combo.currentText()
        
        if category == "全部":
            return None  # Include all sources
        
        # Define exclusive categories
        categories = ["基站", "地宝", "窗宝", "光学组件"]
        
        if category not in categories:
            return None
        
        # Get all sources from bank
        if not self.manager.bank:
            return None
        
        all_sources = list(self.manager.bank.sources)
        
        # Filter: include sources that contain selected category
        # AND exclude sources that contain any other category keywords
        other_categories = [c for c in categories if c != category]
        
        filtered_sources = []
        for source in all_sources:
            # Check if source contains the selected category
            has_selected = category in source
            
            # Check if source contains any other category
            has_other = any(other_cat in source for other_cat in other_categories)
            
            # Include if: (has selected category) OR (doesn't have any category keyword)
            # Exclude if: has other category keywords
            if not has_other:
                filtered_sources.append(source)
        
        return filtered_sources if filtered_sources else None
    
    def init_practice_menu(self):
        self.practice_menu = QWidget()
        layout = QVBoxLayout()
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.setSpacing(20)
        
        title = QLabel("专项练习 - 选择题型")
        title.setFont(QFont("Microsoft YaHei", 20, QFont.Weight.Bold))
        title.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(title)
        
        # Top Selection Area (Grid Layout for tight side-by-side)
        selection_container = QWidget()
        selection_layout = QGridLayout(selection_container)
        selection_layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        selection_layout.setHorizontalSpacing(30) # Adjust for "tightness"
        selection_layout.setVerticalSpacing(10)

        # Left Column: Level Selection
        lbl_level = QLabel("选择等级 (可多选):")
        lbl_level.setFont(QFont("Microsoft YaHei", 14))
        lbl_level.setAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignBottom)
        
        self.practice_level_container = QWidget()
        self.practice_level_layout_inner = QVBoxLayout(self.practice_level_container)
        self.practice_level_layout_inner.setSpacing(5)
        self.practice_level_layout_inner.setContentsMargins(5, 5, 5, 5)
        self.practice_level_checkboxes = []
        
        self.practice_level_scroll = QScrollArea()
        self.practice_level_scroll.setWidgetResizable(True)
        self.practice_level_scroll.setWidget(self.practice_level_container)
        self.practice_level_scroll.setFixedSize(200, 150)
        self.practice_level_scroll.setStyleSheet("border: 1px solid #ccc; border-radius: 5px;")
        
        btn_refresh_practice = QPushButton("刷新题目数量")
        btn_refresh_practice.setFixedWidth(200)
        btn_refresh_practice.clicked.connect(self.refresh_practice_buttons_handler)

        # Right Column: Category Selection (Multi-select, same as Level)
        lbl_practice_category = QLabel("请选择类别 (可多选):")
        lbl_practice_category.setFont(QFont("Microsoft YaHei", 14))
        lbl_practice_category.setAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignBottom)
        
        self.practice_category_container = QWidget()
        self.practice_category_layout_inner = QVBoxLayout(self.practice_category_container)
        self.practice_category_layout_inner.setSpacing(5)
        self.practice_category_layout_inner.setContentsMargins(5, 5, 5, 5)
        self.practice_category_checkboxes = []
        
        self.practice_category_scroll = QScrollArea()
        self.practice_category_scroll.setWidgetResizable(True)
        self.practice_category_scroll.setWidget(self.practice_category_container)
        self.practice_category_scroll.setFixedSize(200, 150)
        self.practice_category_scroll.setStyleSheet("border: 1px solid #ccc; border-radius: 5px;")
        
        # Button to refresh (optional, maybe share the left one or add another? User said "same as left")
        # To keep it clean and "aligned", maybe just one refresh button at the bottom spanning both?
        # Or just keep the left one. The user said "Category selection and exam question selection are the same".
        # Let's add a refresh button for category too or just make the left one refresh everything.
        # The left one is "Refresh Question Count". It applies to ALL filters.
        # So we don't strictly need a second button, but for symmetry/alignment?
        # Let's put the refresh button centered below BOTH lists.
        
        # Add widgets to Grid
        # Row 0: Labels
        selection_layout.addWidget(lbl_level, 0, 0)
        selection_layout.addWidget(lbl_practice_category, 0, 1)
        
        # Row 1: Scroll Areas
        selection_layout.addWidget(self.practice_level_scroll, 1, 0)
        selection_layout.addWidget(self.practice_category_scroll, 1, 1)
        
        # Row 2: Refresh Button (Centered spanning 2 columns)
        selection_layout.addWidget(btn_refresh_practice, 2, 0, 1, 2, Qt.AlignmentFlag.AlignCenter)
        
        layout.addWidget(selection_container)
        layout.addSpacing(20)
        
        # Practice Buttons Area
        self.practice_btn_layout = QVBoxLayout()
        self.practice_btn_layout.setAlignment(Qt.AlignmentFlag.AlignTop | Qt.AlignmentFlag.AlignHCenter)
        self.practice_btn_layout.setSpacing(15)
        layout.addLayout(self.practice_btn_layout)
        
        btn_back = QPushButton("返回主页")
        btn_back.setFixedWidth(200)
        btn_back.setStyleSheet("background-color: #6c757d; color: white; border-radius: 5px; padding: 10px; font-size: 14px;")
        btn_back.clicked.connect(lambda: self.central_widget.setCurrentWidget(self.home_page))
        layout.addWidget(btn_back, 0, Qt.AlignmentFlag.AlignCenter)
        
        self.practice_menu.setLayout(layout)
        self.central_widget.addWidget(self.practice_menu)

    def open_practice_menu(self):
        if not self.manager.bank:
            QMessageBox.warning(self, "警告", "请先加载题库")
            return

        # Populate practice level checkboxes
        # Clear existing
        for i in reversed(range(self.practice_level_layout_inner.count())): 
            self.practice_level_layout_inner.itemAt(i).widget().setParent(None)
        self.practice_level_checkboxes = []
        
        if self.manager.bank:
            for level in self.sort_levels(list(self.manager.bank.levels)):
                chk = QCheckBox(level)
                # Sync with home page selection
                current_home_level = self.level_combo.currentText()
                if level == current_home_level:
                    chk.setChecked(True)
                else:
                    chk.setChecked(False)
                    
                self.practice_level_checkboxes.append(chk)
                self.practice_level_layout_inner.addWidget(chk)
                
            # Populate Category checkboxes (Fixed Categories)
            # Clear existing
            for i in reversed(range(self.practice_category_layout_inner.count())):
                self.practice_category_layout_inner.itemAt(i).widget().setParent(None)
            self.practice_category_checkboxes = []
            
            categories = ["基站", "地宝", "窗宝", "光学组件"]
            current_home_cat = self.category_combo.currentText()
            
            for cat in categories:
                chk = QCheckBox(cat)
                # Sync with home page
                if current_home_cat == "全部":
                    chk.setChecked(True)
                elif current_home_cat == cat:
                    chk.setChecked(True)
                else:
                    chk.setChecked(False)
                    
                self.practice_category_checkboxes.append(chk)
                self.practice_category_layout_inner.addWidget(chk)
        

        
        self.refresh_practice_buttons_handler()
        self.central_widget.setCurrentWidget(self.practice_menu)

    def refresh_practice_buttons_handler(self):
        selected_levels = [c.text() for c in self.practice_level_checkboxes if c.isChecked()]
        selected_categories = [c.text() for c in self.practice_category_checkboxes if c.isChecked()]
        self.refresh_practice_buttons(selected_levels, selected_categories)

    def refresh_practice_buttons(self, levels, categories=None):
        # Clear previous buttons
        for i in reversed(range(self.practice_btn_layout.count())): 
            item = self.practice_btn_layout.itemAt(i)
            if item.widget():
                item.widget().setParent(None)
            
        if not levels:
            return

        # Filter by levels (ANY match)
        all_qs = [q for q in self.manager.bank.questions if any(lvl in q['levels'] for lvl in levels)]
        
        # Filter by categories (Home Page Logic)
        # Logic: Include sources containing selected category keywords
        # PLUS sources that don't contain ANY category keywords (Common sources)
        if categories:
            # 1. Identify all sources
            all_sources = list(self.manager.bank.sources)
            defined_cats = ["基站", "地宝", "窗宝", "光学组件"]
            
            # 2. Find matching sources for selected categories
            # AND find common sources (not matching any defined category)
            allowed_sources = set()
            
            for source in all_sources:
                # Check if source matches any SELECTED category
                is_selected = any(cat in source for cat in categories)
                
                # Check if source matches ANY defined category (to identify common)
                has_any_cat = any(cat in source for cat in defined_cats)
                
                # Include if selected OR it's a common source
                if is_selected or not has_any_cat:
                    allowed_sources.add(source)
            
            all_qs = [q for q in all_qs if q.get('source_sheet') in allowed_sources]
        
        counts = {}
        for q in all_qs:
            counts[q['type']] = counts.get(q['type'], 0) + 1
            
        if not counts:
            lbl_no_data = QLabel("⚠️ 当前选择无题目")
            lbl_no_data.setStyleSheet("color: #666; font-size: 16px; margin-top: 20px;")
            self.practice_btn_layout.addWidget(lbl_no_data)
            return

        # Fixed order: Single, Multi, TrueFalse, Short
        type_order = ['单选题', '多选题', '判断题', '简答题']
        
        for q_type in type_order:
            if q_type in counts and counts[q_type] > 0:
                count = counts[q_type]
                btn = QPushButton(f"{q_type} ({count}题)")
                btn.setFixedWidth(300)
                btn.setStyleSheet("background-color: #007bff; color: white; border-radius: 5px; padding: 10px; font-size: 14px;")
                # Pass categories to start_practice
                btn.clicked.connect(lambda checked, t=q_type: self.start_practice(levels, t, categories))
                self.practice_btn_layout.addWidget(btn, 0, Qt.AlignmentFlag.AlignCenter)

    def start_practice(self, levels, q_type, categories=None):
        # Get all questions first
        all_qs = self.manager.bank.get_all_questions(levels, q_type)
        
        # Filter by source (Same logic as refresh)
        if categories:
            all_sources = list(self.manager.bank.sources)
            defined_cats = ["基站", "地宝", "窗宝", "光学组件"]
            allowed_sources = set()
            
            for source in all_sources:
                is_selected = any(cat in source for cat in categories)
                has_any_cat = any(cat in source for cat in defined_cats)
                
                if is_selected or not has_any_cat:
                    allowed_sources.add(source)

            questions = [q for q in all_qs if q.get('source_sheet') in allowed_sources]
        else:
            questions = all_qs
            
        if not questions:
            QMessageBox.warning(self, "提示", "该题型暂无题目")
            return
            
        self.questions = questions
        self.manager.user_answers = {} # Reset answers for practice session
        self.is_practice_mode = True
        self.current_practice_levels = levels # Store for saving progress
        self.current_index = 0
        self.exam_states = {}
        
        # Setup UI for Practice
        self.central_widget.setCurrentWidget(self.exam_page)
        self.lbl_timer.setVisible(False) # Hide timer
        self.btn_submit_exam.setText("退出练习")
        try:
            self.btn_submit_exam.disconnect()
        except:
            pass
        self.btn_submit_exam.clicked.connect(self.exit_practice)
        
        # Rebuild Nav Grid
        self._build_nav_buttons()
            
        self.load_question(self.current_index)
        
        # Load Progress
        level_key = ",".join(sorted(levels))
        
        if level_key in self.practice_data and q_type in self.practice_data[level_key]:
            data = self.practice_data[level_key][q_type]
            saved_index = data.get('index', 0)
            wrong_indices = data.get('wrong_indices', [])
            
            # Restore wrong states
            for idx in wrong_indices:
                if idx < len(self.questions):
                    self.exam_states[idx] = 'wrong'
            
            if saved_index < len(self.questions):
                self.load_question(saved_index)
            else:
                self.load_question(0)

    def exit_practice(self):
        reply = QMessageBox.question(self, '退出', '确定要退出练习吗？', QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            self.save_current_practice_progress() # Save current index
            self.central_widget.setCurrentWidget(self.home_page)
            # Reset Exam Page state for next time
            self.lbl_timer.setVisible(True)
            self.btn_submit_exam.setText("交卷")
            try:
                self.btn_submit_exam.disconnect()
            except:
                pass
            self.btn_submit_exam.clicked.connect(self.finish_exam)
            self.is_practice_mode = False

    def save_current_practice_progress(self, add_wrong=False, remove_wrong=False):
        if not self.is_practice_mode:
            return
            
        # Use stored levels to generate key
        if hasattr(self, 'current_practice_levels'):
            level_key = ",".join(sorted(self.current_practice_levels))
        else:
            # Fallback if something went wrong, though shouldn't happen
            return
            
        q_type = self.questions[0]['type'] # All questions in practice are same type
        
        if level_key not in self.practice_data:
            self.practice_data[level_key] = {}
        if q_type not in self.practice_data[level_key]:
            self.practice_data[level_key][q_type] = {'index': 0, 'wrong_indices': []}
            
        data = self.practice_data[level_key][q_type]
        data['index'] = self.current_index
        
        if add_wrong:
            if self.current_index not in data['wrong_indices']:
                data['wrong_indices'].append(self.current_index)
        if remove_wrong:
            if self.current_index in data['wrong_indices']:
                data['wrong_indices'].remove(self.current_index)
                
        self.save_practice_progress()

    def start_error_review(self):
        errors = self.manager.error_book.errors
        if not errors:
            QMessageBox.information(self, "提示", "错题本为空，真棒！")
            return

        # Normalize errors to match question format (ensure 'answer' key exists)
        normalized_errors = []
        for e in errors:
            if 'answer' not in e and 'correct_answer' in e:
                e['answer'] = e['correct_answer']
            normalized_errors.append(e)

        self.questions = normalized_errors
        self.is_review_mode = True
        self.is_practice_mode = False # Ensure practice mode is off
        self.current_index = 0
        self.exam_states = {}
        
        # Setup UI for Review
        self.central_widget.setCurrentWidget(self.exam_page)
        self.lbl_timer.setVisible(False)
        self.btn_submit_exam.setText("退出错题本")
        try:
            self.btn_submit_exam.disconnect()
        except:
            pass
        self.btn_submit_exam.clicked.connect(self.exit_review)
        
        self._build_nav_buttons()
        self.load_question(0)

    def exit_review(self):
        self.central_widget.setCurrentWidget(self.home_page)
        self.lbl_timer.setVisible(True)
        self.btn_submit_exam.setText("交卷")
        try:
            self.btn_submit_exam.disconnect()
        except:
            pass
        self.btn_submit_exam.clicked.connect(self.finish_exam)
        self.is_review_mode = False

    def init_exam_page(self):
        self.exam_page = QWidget()
        main_layout = QHBoxLayout()

        # Left Sidebar (Question Navigation)
        self.nav_scroll = QScrollArea()
        self.nav_scroll.setFixedWidth(320) # Increased width for larger buttons
        self.nav_scroll.setWidgetResizable(True)
        self.nav_widget = QWidget()
        self.nav_layout = QVBoxLayout()
        self.nav_widget.setLayout(self.nav_layout)
        self.nav_scroll.setWidget(self.nav_widget)

        # Center (Question Content)
        center_layout = QVBoxLayout()
        
        # Header info
        header_layout = QHBoxLayout()
        self.lbl_progress = QLabel("进度: 1/100")
        self.lbl_timer = QLabel("时间: 00:00") # Timer logic can be added
        header_layout.addWidget(self.lbl_progress)
        header_layout.addStretch()
        self.lbl_timer.setStyleSheet("font-size: 16px; font-weight: bold; color: #333;")
        header_layout.addWidget(self.lbl_timer)
        
        # Timer
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_timer)
        self.remaining_time = 45 * 60 # 45 minutes standard
        
        # Question Text
        self.lbl_question = QLabel("题目内容")
        self.lbl_question.setWordWrap(True)
        self.lbl_question.setFont(QFont("Microsoft YaHei", 16))
        self.lbl_question.setStyleSheet("padding: 20px; background-color: white; border-radius: 10px;")

        # Read Question Button
        self.btn_read = QPushButton("🔊 读题")
        self.btn_read.setFixedWidth(120)
        self.btn_read.setStyleSheet("background-color: #28a745; color: white; border-radius: 5px; padding: 8px; font-size: 14px; margin-left: 20px;")
        self.btn_read.clicked.connect(self.read_current_question)
        
        # Container for question and read button (Optional, but let's just add it to layout)
        # Actually, let's put it in a small HBox with stretch if we want it aligned left or right?
        # User image shows it below the question.
        
        read_btn_layout = QHBoxLayout()
        read_btn_layout.addWidget(self.btn_read)
        read_btn_layout.addStretch()

        # Feedback Label (Hidden by default)
        self.lbl_feedback = QLabel("")
        self.lbl_feedback.setFont(QFont("Microsoft YaHei", 14, QFont.Weight.Bold))
        self.lbl_feedback.setStyleSheet("color: #dc3545; padding: 10px;")
        self.lbl_feedback.setWordWrap(True)  # 启用自动换行，用于显示简答题等长文本答案
        self.lbl_feedback.setMinimumHeight(40)  # 设置最小高度
        self.lbl_feedback.setVisible(False)

        # Options Area
        self.options_area = QWidget()
        self.options_layout = QVBoxLayout()
        self.options_area.setLayout(self.options_layout)

        # Buttons
        btn_layout = QHBoxLayout()
        self.btn_prev = QPushButton("上一题")
        self.btn_prev.clicked.connect(self.prev_question)
        self.btn_next = QPushButton("下一题")
        self.btn_next.clicked.connect(self.next_question)
        self.btn_submit_single = QPushButton("提交本题") # For immediate feedback
        self.btn_submit_single.clicked.connect(self.check_current_answer)
        self.btn_submit_exam = QPushButton("交卷")
        self.btn_submit_exam.setStyleSheet("background-color: #28a745;")
        self.btn_submit_exam.clicked.connect(self.finish_exam)

        btn_layout.addWidget(self.btn_prev)
        btn_layout.addWidget(self.btn_submit_single)
        btn_layout.addWidget(self.btn_next)
        btn_layout.addStretch()
        btn_layout.addWidget(self.btn_submit_exam)

        center_layout.addLayout(header_layout)
        center_layout.addWidget(self.lbl_question)
        center_layout.addLayout(read_btn_layout)
        center_layout.addWidget(self.lbl_feedback)
        center_layout.addWidget(self.options_area)
        center_layout.addStretch()
        center_layout.addLayout(btn_layout)

        main_layout.addWidget(self.nav_scroll)
        main_layout.addLayout(center_layout)
        
        self.exam_page.setLayout(main_layout)
        self.central_widget.addWidget(self.exam_page)

    def init_result_page(self):
        self.result_page = QWidget()
        layout = QVBoxLayout()
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)

        self.lbl_score = QLabel("85分")
        self.lbl_score.setFont(QFont("Arial", 48, QFont.Weight.Bold))
        
        self.lbl_result_msg = QLabel("恭喜你，考试及格！")
        self.lbl_result_msg.setFont(QFont("Microsoft YaHei", 20))

        btn_retry = QPushButton("再来一遍")
        btn_retry.setFixedWidth(200)
        btn_retry.clicked.connect(lambda: self.central_widget.setCurrentWidget(self.home_page))

        btn_exit = QPushButton("退出")
        btn_exit.setFixedWidth(200)
        btn_exit.clicked.connect(lambda: self.central_widget.setCurrentWidget(self.home_page))

        layout.addWidget(QLabel("考试结束，您的得分是："))
        layout.addWidget(self.lbl_score)
        layout.addWidget(self.lbl_result_msg)
        layout.addSpacing(30)
        layout.addWidget(btn_retry)
        layout.addWidget(btn_exit)

        self.result_page.setLayout(layout)
        self.central_widget.addWidget(self.result_page)

    def start_mock_exam(self):
        if not self.manager.bank:
            QMessageBox.warning(self, "警告", "请先加载题库")
            return

        level = self.level_combo.currentText()
        if not level or level == "请先加载题库":
            QMessageBox.warning(self, "警告", "请先选择等级")
            return
        
        # Get selected sources
        selected_sources = self.get_selected_sources()
        # Note: selected_sources can be None (meaning all sources) or a list
            
        self.questions = self.manager.start_exam(level, selected_sources)
        if not self.questions:
            QMessageBox.critical(self, "错误", "该等级题库不足，无法开始考试")
            return

        self.is_practice_mode = False  # 明确设置为考试模式
        self.current_index = 0
        self.exam_states = {} # {index: 'correct'/'wrong'}
        
        self._build_nav_buttons()

        self.central_widget.setCurrentWidget(self.exam_page)
        
        # Start Timer
        self.remaining_time = 45 * 60
        self.timer.start(1000)
        self.update_timer()
        
        self.load_question(0)

    def _build_nav_buttons(self):
        # Clear existing layout items
        while self.nav_layout.count():
            item = self.nav_layout.takeAt(0)
            widget = item.widget()
            if widget:
                widget.deleteLater()
                
        self.nav_buttons = []
        current_type = None
        current_grid = None
        grid_index = 0
        
        for i, q in enumerate(self.questions):
            q_type = q['type']
            
            if q_type != current_type:
                current_type = q_type
                grid_index = 0
                
                # Add Header
                header = QLabel(q_type)
                header.setStyleSheet("font-weight: bold; font-size: 14px; margin-top: 10px; margin-bottom: 5px;")
                self.nav_layout.addWidget(header)
                
                # Add Grid Container
                container = QWidget()
                current_grid = QGridLayout()
                current_grid.setSpacing(5)
                current_grid.setContentsMargins(0, 0, 0, 0)
                container.setLayout(current_grid)
                self.nav_layout.addWidget(container)
            
            # Add Button
            btn = QPushButton(str(i+1))
            btn.setFixedSize(50, 35) # Increased width to 50
            btn.setStyleSheet("background-color: #e0e0e0; color: black; border: none; border-radius: 3px;")
            btn.clicked.connect(lambda checked, idx=i: self.jump_to_question(idx))
            
            current_grid.addWidget(btn, grid_index // 5, grid_index % 5)
            self.nav_buttons.append(btn)
            grid_index += 1
            
        self.nav_layout.addStretch() # Push everything up

    def update_timer(self):
        # Decrement time first
        self.remaining_time -= 1
        
        # Display current time
        minutes = self.remaining_time // 60
        seconds = self.remaining_time % 60
        self.lbl_timer.setText(f"剩余时间: {minutes:02d}:{seconds:02d}")
        
        # Warning color if less than 5 mins
        if self.remaining_time < 300:
            self.lbl_timer.setStyleSheet("font-size: 16px; font-weight: bold; color: red;")
        else:
            self.lbl_timer.setStyleSheet("font-size: 16px; font-weight: bold; color: #333;")
        
        # Check if time is up
        if self.remaining_time <= 0:
            self.timer.stop()
            self.finish_exam(auto=True)

    def load_question(self, index):
        # Stop any pending auto-next timer to prevent race conditions
        if hasattr(self, 'auto_next_timer'):
            self.auto_next_timer.stop()
        
        self.current_index = index
        q = self.questions[index]
        
        self.lbl_progress.setText(f"进度: {index + 1}/{len(self.questions)}  [{q['type']}]")
        
        source = q.get('source_sheet', '未知来源')
        # Find which level this question actually belongs to (intersection of q['levels'])
        # Since a question can belong to multiple levels, we just show all of them or the relevant one?
        # Let's show all levels it belongs to.
        q_levels = ",".join(q.get('levels', []))
        
        # Different font/color for source
        source_html = f"<span style='color: #666; font-size: 14px; font-family: Segoe UI;'>  (来源: {source} | 等级: {q_levels})</span>"
        self.lbl_question.setText(f"{index + 1}. {q['question']}{source_html}")

        # Clear options and feedback
        self.lbl_feedback.setVisible(False)
        for i in reversed(range(self.options_layout.count())): 
            self.options_layout.itemAt(i).widget().setParent(None)

        self.option_group = QButtonGroup(self)
        self.option_group.setExclusive(q['type'] in ['单选题', '判断题'])
        
        # Show Options
        if q['type'] in ['单选题', '多选题']:
            for key, val in sorted(q['options'].items()):
                btn = QRadioButton(f"{key}. {val}") if q['type'] == '单选题' else QCheckBox(f"{key}. {val}")
                # 增大按钮字体和padding,提升点击体验
                btn.setStyleSheet("QRadioButton, QCheckBox { font-size: 18px; padding: 12px; } QRadioButton::indicator, QCheckBox::indicator { width: 20px; height: 20px; }")
                self.options_layout.addWidget(btn)
                self.option_group.addButton(btn)
        elif q['type'] == '判断题':
            # Force simplified options
            # Check if the answer matches a key (A/B) or a value (√/×/Text)
            use_key_as_answer = q['answer'] in q['options'].keys()
            
            for key, val in sorted(q['options'].items()):
                btn_text = val
                if val in ['正确', 'True', 'T', '对', '√']:
                    btn_text = "√"
                elif val in ['错误', 'False', 'F', '错', '×']:
                    btn_text = "×"
                
                btn = QRadioButton(btn_text)
                
                # If answer is a key (e.g. 'A'), submit 'A'.
                # If answer is a value (e.g. '×'), submit the value '×' (or original 'val' if it matches).
                # In the inspected file, options are {'A': '√', 'B': '×'} and answer is '×'.
                # So we should submit 'val'.
                
                if use_key_as_answer:
                    btn.setProperty("option_key", key)
                else:
                    btn.setProperty("option_key", val)
                    
                # 判断题按钮样式统一优化
                btn.setStyleSheet("QRadioButton { font-size: 24px; font-weight: bold; padding: 12px; }")
                self.options_layout.addWidget(btn)
                self.option_group.addButton(btn)
        elif q['type'] == '简答题':
            hint_label = QLabel("(简答题请在心中作答，点击提交查看参考答案)")
            hint_label.setWordWrap(True)
            hint_label.setStyleSheet("color: #666; font-size: 12px; padding: 10px;")
            self.options_layout.addWidget(hint_label)
        
        # Connect signals for auto-feedback (Single/TF)
        if q['type'] in ['单选题', '判断题']:
            for btn in self.option_group.buttons():
                btn.clicked.connect(self.check_current_answer)
        
        # Restore user answer if exists (and disable if already answered)
        if index in self.manager.user_answers:
            user_ans = self.manager.user_answers[index]
            # Logic to restore UI state (check buttons)
            # And show feedback immediately if already answered
            self.restore_answer_state(q, user_ans)
        elif self.is_review_mode:
            # In review mode, show the wrong answer the user gave (stored in 'user_answer' field of error object)
            # And show correct answer
            user_ans = q.get('user_answer', '')
            self.restore_answer_state(q, user_ans, review_mode=True)

        # Restore user answer if exists
        # (Simplification: Not fully restoring UI state for Multi-select/Text for now, just clearing)

        # Highlight current nav button
        for idx, btn in enumerate(self.nav_buttons):
            style = ""
            
            # Base color based on state
            if idx in self.exam_states:
                if self.exam_states[idx] == 'correct':
                    style += "background-color: #28a745; color: white;"
                else:
                    style += "background-color: #dc3545; color: white;"
            else:
                style += "background-color: #e0e0e0; color: black;"

            # Current selection styling
            if idx == index:
                if idx not in self.exam_states:
                    # If current and not answered, use standard blue
                    style = "background-color: #007bff; color: white; border: none;"
                else:
                    # If current and answered, keep state color but add border
                    style += "border: 3px solid #007bff;"
            else:
                style += "border: none;"
            
            btn.setStyleSheet(style)

        # Update Submit Button Text based on type
        if q['type'] == '多选题':
            self.btn_submit_single.setText("确认答案")
            self.btn_submit_single.setVisible(True)
        elif q['type'] == '简答题':
            self.btn_submit_single.setText("查看答案")
            self.btn_submit_single.setVisible(True)
        else:
            # For Single/TF, it auto-submits on click, but button can remain as "Check" or hidden?
            # Current logic: Single/TF auto-submits. Button is redundant but harmless.
            self.btn_submit_single.setVisible(False)
        
        # 延迟500ms自动读题
        if hasattr(self, 'auto_read_timer'):
            self.auto_read_timer.stop()  # 停止之前的定时器
            self.auto_read_timer.start(300)  # 启动新的500ms定时器

    def jump_to_question(self, index):
        self.auto_check_if_needed()
        self.load_question(index)

    def prev_question(self):
        self.auto_check_if_needed()
        if self.current_index > 0:
            self.load_question(self.current_index - 1)

    def next_question(self):
        if self.auto_check_if_needed():
            return # Pause to show feedback
        if self.current_index < len(self.questions) - 1:
            self.load_question(self.current_index + 1)

    def auto_check_if_needed(self):
        # If current question is Multi-choice and has selections but not submitted (no state), check it.
        # Only in Practice Mode? User wants feedback.
        if self.current_index >= len(self.questions): return False
        
        q = self.questions[self.current_index]
        if q['type'] == '多选题' and self.current_index not in self.exam_states:
            # Check if any option is selected
            has_selection = False
            for i in range(self.options_layout.count()):
                widget = self.options_layout.itemAt(i).widget()
                if isinstance(widget, QCheckBox) and widget.isChecked():
                    has_selection = True
                    break
            
            if has_selection:
                self.check_current_answer()
                return True # Checked and paused
        return False

    def restore_answer_state(self, q, user_ans, review_mode=False):
        # Restore checks
        if q['type'] in ['单选题', '判断题']:
            for btn in self.option_group.buttons():
                # Check property or text
                key = btn.property("option_key")
                if not key:
                    key = btn.text().split('.')[0]
                    
                if key in user_ans:
                    btn.setChecked(True)
                btn.setEnabled(False) # Disable since already answered
        elif q['type'] == '多选题':
             for i in range(self.options_layout.count()):
                widget = self.options_layout.itemAt(i).widget()
                if isinstance(widget, QCheckBox):
                    key = widget.text().split('.')[0]
                    if key in user_ans:
                        widget.setChecked(True)
        
        # Show feedback again
        correct_ans = q['answer']
        is_correct = user_ans == correct_ans
        
        if is_correct:
            self.lbl_feedback.setText("√ 回答正确！")
            self.lbl_feedback.setStyleSheet("color: #28a745; font-weight: bold; font-size: 16px;")
            self.lbl_feedback.setVisible(True)
            self._highlight_options(correct_ans, True)
        else:
            self.lbl_feedback.setText(f"× 回答错误！ 正确答案: {correct_ans}")
            self.lbl_feedback.setStyleSheet("color: #dc3545; font-weight: bold; font-size: 16px;")
            self.lbl_feedback.setVisible(True)
            self._highlight_options(correct_ans, False, user_ans)

    def check_current_answer(self):
        # Get answer from UI
        q = self.questions[self.current_index]
        user_ans = []
        
        if q['type'] in ['单选题', '判断题']:
            for btn in self.option_group.buttons():
                if btn.isChecked():
                    # Use property if available (for T/F), else parse text
                    key = btn.property("option_key")
                    if key:
                        user_ans.append(key)
                    else:
                        user_ans.append(btn.text().split('.')[0])
        elif q['type'] == '多选题':
            # For checkboxes, we need to iterate layout children as QButtonGroup doesn't support multi-check well for getChecked
            # Actually QButtonGroup with setExclusive(False) works for logical grouping but buttons() returns all.
            # Let's just iterate widgets in layout
            for i in range(self.options_layout.count()):
                widget = self.options_layout.itemAt(i).widget()
                if isinstance(widget, QCheckBox) and widget.isChecked():
                    user_ans.append(widget.text().split('.')[0])
        
        final_ans = "".join(sorted(user_ans))
        self.manager.submit_answer(self.current_index, final_ans)
        
        # Show feedback
        correct_ans = q['answer']
        is_correct = final_ans == correct_ans
        
        # Disable inputs after answer for Single/TF to prevent changing
        # Only disable if correct or if it's Mock Exam. 
        # In Practice Mode, if wrong, we might want to allow retry?
        # Requirement: "wrong marks persistent until answered correctly" implies we can answer again.
        # So if Practice Mode and Wrong, do NOT disable?
        if q['type'] in ['单选题', '判断题']:
            should_disable = True
            if self.is_practice_mode and not is_correct:
                should_disable = False
            
            if should_disable:
                for btn in self.option_group.buttons():
                    btn.setEnabled(False)
        
        if is_correct:
            self.lbl_feedback.setText("√ 回答正确！")
            self.lbl_feedback.setStyleSheet("color: #28a745; font-weight: bold; font-size: 16px;")
            self.lbl_feedback.setVisible(True)
            self.nav_buttons[self.current_index].setStyleSheet("background-color: #28a745; color: white;")
            
            # Highlight selected correct option
            self._highlight_options(correct_ans, True)
            
            # Auto-jump to next question after 1.5 seconds if answer is correct
            if self.current_index < len(self.questions) - 1:
                self.auto_next_timer.start(1500)
        else:
            self.lbl_feedback.setText(f"× 回答错误！ 正确答案: {correct_ans}")
            self.lbl_feedback.setStyleSheet("color: #dc3545; font-weight: bold; font-size: 16px;")
            self.lbl_feedback.setVisible(True)
            self.nav_buttons[self.current_index].setStyleSheet("background-color: #dc3545; color: white;")
            
            # Highlight wrong and correct options
            self._highlight_options(correct_ans, False, final_ans)
            
            # 答错不自动跳转,停留在当前题让用户查看反馈

        # Update State
        if is_correct:
            self.exam_states[self.current_index] = 'correct'
            self.nav_buttons[self.current_index].setStyleSheet("background-color: #28a745; color: white; border: 3px solid #007bff;")
            if self.is_practice_mode:
                 self.save_current_practice_progress(remove_wrong=True)
        else:
            self.exam_states[self.current_index] = 'wrong'
            self.nav_buttons[self.current_index].setStyleSheet("background-color: #dc3545; color: white; border: 3px solid #007bff;")
            if self.is_practice_mode:
                 self.save_current_practice_progress(add_wrong=True)

    def _highlight_options(self, correct_ans, is_correct, user_ans=None):
        # Iterate over all options in layout
        for i in range(self.options_layout.count()):
            widget = self.options_layout.itemAt(i).widget()
            if not isinstance(widget, (QRadioButton, QCheckBox)):
                continue
                
            text_key = widget.text().split('.')[0]
            
            # Reset style first - 保持与按钮创建时一致的字体大小
            if isinstance(widget, QRadioButton) and widget.text() in ['√', '×']:
                # 判断题特殊样式
                widget.setStyleSheet("QRadioButton { font-size: 24px; font-weight: bold; padding: 12px; }")
            else:
                widget.setStyleSheet("font-size: 18px; padding: 12px;")
            
            # Highlight Correct Answer (Green)
            if text_key in correct_ans:
                if isinstance(widget, QRadioButton) and widget.text() in ['√', '×']:
                    widget.setStyleSheet("QRadioButton { font-size: 24px; padding: 12px; color: #28a745; font-weight: bold; }")
                else:
                    widget.setStyleSheet("font-size: 18px; padding: 12px; color: #28a745; font-weight: bold;")
            
            # Highlight Wrong User Answer (Red) - only if not correct
            if not is_correct and user_ans and text_key in user_ans and text_key not in correct_ans:
                if isinstance(widget, QRadioButton) and widget.text() in ['√', '×']:
                    widget.setStyleSheet("QRadioButton { font-size: 24px; padding: 12px; color: #dc3545; font-weight: bold; text-decoration: line-through; }")
                else:
                    widget.setStyleSheet("font-size: 18px; padding: 12px; color: #dc3545; font-weight: bold; text-decoration: line-through;")

    def finish_exam(self):
        reply = QMessageBox.question(self, '交卷', '确定要交卷吗？', QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            result = self.manager.calculate_result()
            self.show_result(result)

    def show_result(self, result):
        self.lbl_score.setText(f"{result['total_score']}分")
        if result['passed']:
            self.lbl_score.setStyleSheet("color: #28a745;")
            self.lbl_result_msg.setText("恭喜你，考试及格！")
        else:
            self.lbl_score.setStyleSheet("color: #dc3545;")
            self.lbl_result_msg.setText("很遗憾，考试不及格。")
            
        self.central_widget.setCurrentWidget(self.result_page)

    def load_practice_progress(self):
        if os.path.exists(self.practice_file):
            try:
                with open(self.practice_file, 'r', encoding='utf-8') as f:
                    return json.load(f)
            except:
                return {}
        return {}

    def save_practice_progress(self):
        try:
            with open(self.practice_file, 'w', encoding='utf-8') as f:
                json.dump(self.practice_data, f, ensure_ascii=False, indent=4)
        except Exception as e:
            print(f"Error saving practice progress: {e}")

    def save_current_practice_progress(self, add_wrong=False, remove_wrong=False):
        if not self.is_practice_mode:
            return
            
        level = self.level_combo.currentText()
        q_type = self.questions[0]['type'] # All questions in practice are same type
        
        if level not in self.practice_data:
            self.practice_data[level] = {}
        if q_type not in self.practice_data[level]:
            self.practice_data[level][q_type] = {'index': 0, 'wrong_indices': []}
            
        data = self.practice_data[level][q_type]
        data['index'] = self.current_index
        
        if add_wrong:
            if self.current_index not in data['wrong_indices']:
                data['wrong_indices'].append(self.current_index)
        if remove_wrong:
            if self.current_index in data['wrong_indices']:
                data['wrong_indices'].remove(self.current_index)
                
        self.save_practice_progress()

    def load_custom_bank(self):
        from main_code.excel_loader import QuestionBank
        
        file_path, _ = QFileDialog.getOpenFileName(self, "选择题库文件", "", "Excel Files (*.xlsx)")
        if not file_path:
            return
            
        self.load_bank_file(file_path)

    def load_bank_file(self, file_path, silent=False):
        from main_code.excel_loader import QuestionBank
        try:
            # Get sheet names
            sheets = QuestionBank.get_all_sheet_names(file_path)
            
            if not sheets:
                if not silent: QMessageBox.warning(self, "错误", "无法读取文件或文件为空")
                return
            
            # If interactive, show dialog. If silent, skip dialog.
            selected_sheets = None
            if not silent:
                dialog = SheetSelectionDialog(sheets, self)
                if dialog.exec() == QDialog.DialogCode.Accepted:
                    selected_sheets = dialog.get_selected_sheets()
                else:
                    return
            
            new_bank = QuestionBank(file_path, selected_sheets)
            if not new_bank.questions:
                if not silent: QMessageBox.warning(self, "警告", "未找到有效题目")
                return
                
            self.manager.load_bank(new_bank)
            
            # Update UI
            self.level_combo.clear()
            self.level_combo.setEnabled(True)
            self.level_combo.addItems(self.sort_levels(list(self.manager.bank.levels)))
            
            self.btn_mock_exam.setEnabled(True)
            self.btn_practice.setEnabled(True)
            
            # Save to config
            try:
                with open("config.json", 'w', encoding='utf-8') as f:
                    json.dump({'last_bank_path': file_path}, f)
            except:
                pass
            
            if not silent:
                # Calculate breakdown
                level_counts = {}
                for q in new_bank.questions:
                    for level in q['levels']:
                        level_counts[level] = level_counts.get(level, 0) + 1
                
                msg = f"共加载到 {len(new_bank.questions)} 道题\n"
                for level in sorted(level_counts.keys()):
                    msg += f"{level}：{level_counts[level]}道\n"
                
                QMessageBox.information(self, "成功", msg.strip())
                
        except Exception as e:
            if not silent: QMessageBox.critical(self, "错误", f"加载失败: {e}")

    def read_current_question(self):
        if self.current_index >= len(self.questions):
            return
            
        q = self.questions[self.current_index]
        text_to_read = q['question']
        
        # Kill previous process if running
        if self.tts_process and self.tts_process.poll() is None:
            try:
                self.tts_process.kill()
                self.tts_process.wait(timeout=0.1)
            except:
                pass
        
        # Start new process
        try:
            # Call the main executable (or python script) with the worker flag
            # This works for both running from source and frozen exe
            cmd = [sys.executable]
            
            # If running from source (not frozen), we need to pass the script path
            if not getattr(sys, 'frozen', False):
                 cmd.append(os.path.join(os.path.dirname(os.path.dirname(__file__)), 'main.py'))
            
            cmd.append('--tts-worker')
            
            # CREATE_NO_WINDOW = 0x08000000
            creation_flags = 0x08000000 if sys.platform == 'win32' else 0
            
            self.tts_process = subprocess.Popen(
                cmd,
                stdin=subprocess.PIPE,
                creationflags=creation_flags
            )
            
            # Send text via stdin
            if self.tts_process.stdin:
                self.tts_process.stdin.write(text_to_read.encode('utf-8'))
                self.tts_process.stdin.close()
                
        except Exception as e:
            print(f"TTS Process Error: {e}")
        
    # Removed _speak_text as it's now in TTSThread

if __name__ == "__main__":
    # For testing UI standalone (needs mock manager)
    app = QApplication(sys.argv)
    # Mock manager would be needed here
    pass
