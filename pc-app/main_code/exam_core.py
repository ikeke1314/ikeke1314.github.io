import json
import os
import random
from datetime import datetime

class ErrorBook:
    def __init__(self, file_path='error_questions.json'):
        self.file_path = file_path
        self.errors = self.load_errors()

    def load_errors(self):
        if os.path.exists(self.file_path):
            try:
                with open(self.file_path, 'r', encoding='utf-8') as f:
                    return json.load(f)
            except Exception as e:
                print(f"Error loading error book: {e}")
                return []
        return []

    def add_error(self, question, user_answer):
        error_entry = {
            'question': question['question'],
            'type': question['type'],
            'options': question['options'],
            'correct_answer': question['answer'],
            'user_answer': user_answer,
            'source_sheet': question.get('source_sheet', 'Unknown'),
            'timestamp': datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        }
        self.errors.append(error_entry)
        self.save_errors()

    def save_errors(self):
        try:
            with open(self.file_path, 'w', encoding='utf-8') as f:
                json.dump(self.errors, f, ensure_ascii=False, indent=4)
        except Exception as e:
            print(f"Error saving error book: {e}")

class ExamManager:
    def __init__(self, question_bank):
        self.bank = question_bank
        self.current_questions = []
        self.user_answers = {} # {index: answer}
        self.error_book = ErrorBook()
        self.exam_config = {
            '单选题': 40,
            '多选题': 10,
            '判断题': 10,
            '简答题': 3
        }
        self.scores = {
            '单选题': 1,
            '多选题': 2,
            '判断题': 1,
            '简答题': 10
        }
    
    def load_bank(self, question_bank):
        """Load or replace the question bank."""
        self.bank = question_bank

    def start_exam(self, target_level, selected_sources=None):
        """
        Start a new exam for the given target level with weighted distribution.
        target_level: string (e.g., '一级')
        selected_sources: list of sources to filter by
        Returns: List of questions (flat list)
        """
        if not self.bank:
            return []
            
        self.user_answers = {}
        
    # Define Weights (Exam Level -> {Source Level: Weight})
    LEVEL_WEIGHTS = {
        '一级': {'一级': 0.8, '二级': 0.2},
        '二级': {'一级': 0.1, '二级': 0.7, '三级': 0.2},
        '三级': {'二级': 0.1, '三级': 0.7, '四级': 0.2},
        '四级': {'三级': 0.1, '四级': 0.7, '五级': 0.2},
        '五级': {'四级': 0.1, '五级': 0.7, '六级': 0.2},
        '六级': {'五级': 0.1, '六级': 0.9}
    }

    def start_exam(self, target_level, selected_sources=None):
        """
        Start a new exam for the given target level with weighted distribution.
        target_level: string (e.g., '一级')
        selected_sources: list of sources to filter by
        Returns: List of questions (flat list)
        """
        if not self.bank:
            return []
            
        self.user_answers = {}
        
        level_weights = self.LEVEL_WEIGHTS.get(target_level, {target_level: 1.0})
        
        final_questions = []
        seen_questions = set() # Track unique questions by text
        
        # We need to fulfill the exam config counts (e.g. 40 Single, 10 Multi...)
        # For each type, distribute the count according to weights
        
        for q_type, total_count in self.exam_config.items():
            # Calculate counts per level
            # Use a remainder approach to ensure total matches
            
            # First pass: floor counts
            type_questions = []
            counts_per_level = {}
            current_total = 0
            
            sorted_levels = sorted(level_weights.keys())
            
            for lvl in sorted_levels:
                w = level_weights[lvl]
                count = int(total_count * w)
                counts_per_level[lvl] = count
                current_total += count
                
            # Distribute remainder to the main level (target_level) or highest weight
            remainder = total_count - current_total
            if remainder > 0:
                # Add to target level if present, else max weight level
                if target_level in counts_per_level:
                    counts_per_level[target_level] += remainder
                else:
                    # Fallback to first level
                    counts_per_level[sorted_levels[0]] += remainder
            
            # Fetch questions for this type from each level
            for lvl, count in counts_per_level.items():
                if count <= 0:
                    continue
                    
                # Use bank to get questions for this specific level and type
                temp_config = {q_type: 999}  # Get all available
                
                qs_dict = self.bank.get_questions(lvl, temp_config, selected_sources)
                
                if q_type in qs_dict and len(qs_dict[q_type]) > 0:
                    available_qs = qs_dict[q_type]
                    
                    # Filter out already seen questions to prevent duplicates
                    # Use question text as unique identifier
                    unique_available = [q for q in available_qs if q['question'] not in seen_questions]
                    
                    # If we ran out of unique questions, we might have to reuse (but try to avoid)
                    # For now, just take what we can get from unique ones
                    
                    if len(unique_available) >= count:
                        selected = random.sample(unique_available, count)
                    else:
                        selected = unique_available # Take all remaining unique ones
                        
                    # Add to seen set
                    for q in selected:
                        seen_questions.add(q['question'])
                        
                    type_questions.extend(selected)
            
            # Shuffle the combined questions for this type
            random.shuffle(type_questions)
            final_questions.extend(type_questions)
            
        self.current_questions = final_questions
        return self.current_questions

    def submit_answer(self, index, answer):
        """
        Record user answer.
        """
        if 0 <= index < len(self.current_questions):
            self.user_answers[index] = answer

    def calculate_result(self):
        """
        Calculate score and process errors.
        Returns: dict with score details
        """
        total_score = 0
        max_score = 0
        results = []
        
        for i, q in enumerate(self.current_questions):
            user_ans = self.user_answers.get(i, "")
            correct_ans = q['answer']
            q_type = q['type']
            points = self.scores.get(q_type, 0)
            
            # Normalize answers for comparison (e.g., strip whitespace, uppercase)
            # For Multi-choice, sort characters if needed (assuming 'ABC' == 'CBA' is not standard, usually 'ABC' is required)
            # But usually options are fixed. Let's assume simple string match for now.
            
            is_correct = False
            if q_type == '简答题':
                # For Short Answer, we might not be able to auto-grade perfectly.
                # For now, assume if not empty, give full marks? Or maybe 0 for manual review?
                # Requirement says "submit... calculate total score". 
                # Usually auto-grading short answers is hard. 
                # Let's assume strict match is too harsh. 
                # Maybe just check if length > 0 for now, or strict match?
                # Given it's a "Mock Exam", usually it's strict or self-graded.
                # Let's go with strict match for now, but maybe log it.
                is_correct = user_ans.strip() == correct_ans.strip()
            else:
                is_correct = user_ans.strip() == correct_ans.strip()

            if is_correct:
                total_score += points
            else:
                # Record error
                self.error_book.add_error(q, user_ans)
            
            max_score += points
            
            results.append({
                'index': i,
                'question': q,
                'user_answer': user_ans,
                'is_correct': is_correct,
                'points': points if is_correct else 0
            })

        passed = total_score >= 80
        
        return {
            'total_score': total_score,
            'max_score': max_score,
            'passed': passed,
            'details': results
        }

if __name__ == "__main__":
    # Test Logic
    from excel_loader import QuestionBank
    
    print("Loading Bank...")
    bank = QuestionBank("exam_bank/技能士题库_汇总.xlsx")
    manager = ExamManager(bank)
    
    print("Starting Exam Level 1...")
    qs = manager.start_exam('一级')
    print(f"Exam started with {len(qs)} questions.")
    
    # Simulate answering
    print("Simulating answers...")
    for i, q in enumerate(qs):
        # Answer correctly for first 10, wrong for others
        if i < 10:
            manager.submit_answer(i, q['answer'])
        else:
            manager.submit_answer(i, "WRONG")
            
    result = manager.calculate_result()
    print(f"Score: {result['total_score']}/{result['max_score']}")
    print(f"Passed: {result['passed']}")
    print(f"Error book saved to {manager.error_book.file_path}")
