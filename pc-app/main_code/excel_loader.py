import pandas as pd
import os
import random

class QuestionBank:
    def __init__(self, file_path, selected_sheets=None):
        self.file_path = file_path
        self.selected_sheets = selected_sheets
        self.questions = []
        self.levels = set()
        self.sources = set()  # Track all sources
        self.load_data()

    @staticmethod
    def get_all_sheet_names(file_path):
        if not os.path.exists(file_path):
             return []
        try:
            xl = pd.ExcelFile(file_path)
            return xl.sheet_names
        except:
            return []

    def load_data(self):
        if not os.path.exists(self.file_path):
            raise FileNotFoundError(f"Question bank file not found: {self.file_path}")

        try:
            xl = pd.ExcelFile(self.file_path)
            
            if self.selected_sheets:
                valid_sheets = [s for s in xl.sheet_names if s in self.selected_sheets]
            else:
                # Allow 'Sheet1' or sheets with "考核题库"
                valid_sheets = [s for s in xl.sheet_names if "考核题库" in s or s == "Sheet1"]
                # Filter out "透视" sheets
                valid_sheets = [s for s in valid_sheets if "透视" not in s]
            
            print(f"Loading sheets: {valid_sheets}")

            for sheet_name in valid_sheets:
                # Read header from row 4 (index 3)
                df = xl.parse(sheet_name, header=3)
                
                # Normalize columns
                df.columns = [str(c).strip() for c in df.columns]
                
                required_cols = ['考题类型', '题目', '答案']
                if not all(col in df.columns for col in required_cols):
                    print(f"Skipping sheet {sheet_name}: Missing required columns")
                    continue

                # Filter out empty rows
                df = df.dropna(subset=['题目', '答案'])

                for _, row in df.iterrows():
                    q_type = str(row['考题类型']).strip()
                    question = str(row['题目']).strip()
                    answer = str(row['答案']).strip()
                    
                    # Extract Options
                    options = {}
                    for opt in ['A', 'B', 'C', 'D', 'E']:
                        col_name = f'选项{opt}'
                        if col_name in df.columns and pd.notna(row[col_name]):
                            options[opt] = str(row[col_name]).strip()
                    
                    # Determine Levels
                    q_levels = []
                    level_cols = ['一级', '二级', '三级', '四级', '五级', '六级']
                    for lvl in level_cols:
                        if lvl in df.columns and pd.notna(row[lvl]):
                            # Check if marked (e.g., with a checkmark or any non-empty value)
                            q_levels.append(lvl)
                            self.levels.add(lvl)

                    # Extract Source
                    source = sheet_name
                    if '来源' in df.columns:
                        val = row['来源']
                        if pd.notna(val):
                            source = str(val).strip()
                    
                    self.sources.add(source)  # Track source


                    q_data = {
                        'type': q_type,
                        'question': question,
                        'options': options,
                        'answer': answer,
                        'levels': q_levels,
                        'source_sheet': source
                    }
                    self.questions.append(q_data)
            
            print(f"Loaded {len(self.questions)} questions.")
            print(f"Available levels: {sorted(list(self.levels))}")

        except Exception as e:
            print(f"Error loading Excel: {e}")
            raise

    def get_questions(self, levels, q_types_counts, selected_sources=None):
        """
        Get random questions for specific level(s).
        levels: single string or list of strings (e.g. ['一级', '二级'])
        q_types_counts: dict { '单选题': 40, '多选题': 10, ... }
        selected_sources: list of sources to filter by, None means all sources
        Returns: dict { '单选题': [q1, q2...], ... }
        """
        selected_questions = {}
        
        # Ensure levels is a list
        if isinstance(levels, str):
            levels = [levels]
            
        # Filter questions by level (match ANY of the selected levels)
        # Question 'levels' is a list of levels that question belongs to.
        # We want questions where set(question.levels) intersection set(selected_levels) is not empty.
        level_questions = [
            q for q in self.questions 
            if any(lvl in q['levels'] for lvl in levels)
        ]
        
        # Filter by sources if specified
        if selected_sources:
            level_questions = [q for q in level_questions if q.get('source_sheet') in selected_sources]
        
        for q_type, count in q_types_counts.items():
            type_qs = [q for q in level_questions if q['type'] == q_type]
            
            if len(type_qs) < count:
                print(f"Warning: Insufficient questions for {levels} - {q_type}. Required: {count}, Available: {len(type_qs)}")
                selected_questions[q_type] = type_qs # Return all available
                random.shuffle(selected_questions[q_type])
            else:
                selected_questions[q_type] = random.sample(type_qs, count)
                
        return selected_questions

    def check_sufficiency(self, level, requirements):
        """
        Check if there are enough questions for a level.
        requirements: dict { '单选题': 40, ... }
        Returns: dict { '单选题': (available, required, is_enough), ... }
        """
        # Keep this simple for now, usually used for single level check but let's support list if needed
        if isinstance(level, str):
            level = [level]
            
        level_questions = [
            q for q in self.questions 
            if any(lvl in q['levels'] for lvl in level)
        ]
        status = {}
        
        for q_type, count in requirements.items():
            type_qs = [q for q in level_questions if q['type'] == q_type]
            status[q_type] = (len(type_qs), count, len(type_qs) >= count)
            
        return status

    def get_counts_by_type(self, level):
        """
        Get question counts for each type for a specific level.
        Returns: dict { '单选题': 120, ... }
        """
        if isinstance(level, str):
            level = [level]

        level_questions = [
            q for q in self.questions 
            if any(lvl in q['levels'] for lvl in level)
        ]
        counts = {}
        for q in level_questions:
            q_type = q['type']
            counts[q_type] = counts.get(q_type, 0) + 1
        return counts

    def get_all_questions(self, level, q_type):
        """
        Get all questions of a specific type for a level (or levels).
        """
        if isinstance(level, str):
            level = [level]
            
        return [
            q for q in self.questions 
            if any(lvl in q['levels'] for lvl in level) and q['type'] == q_type
        ]

if __name__ == "__main__":
    # Test run
    bank = QuestionBank("exam_bank/技能士题库_汇总.xlsx")
    
    # Test sufficiency for Level 1
    reqs = {'单选题': 40, '多选题': 10, '判断题': 10, '简答题': 3}
    print("\nSufficiency Check for 一级:")
    print(bank.check_sufficiency('一级', reqs))
    
    # Test fetching
    print("\nFetching questions for 一级:")
    qs = bank.get_questions('一级', reqs)
    for q_type, q_list in qs.items():
        print(f"{q_type}: {len(q_list)} questions")
