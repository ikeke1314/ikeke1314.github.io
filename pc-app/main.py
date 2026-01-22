import sys
import os
from PyQt6.QtWidgets import QApplication, QMessageBox

# Add project root to path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from main_code.excel_loader import QuestionBank
from main_code.exam_core import ExamManager
from main_code.ui_build import MainWindow

def run_tts_worker():
    import traceback
    try:
        import win32com.client
        import pythoncom
    except ImportError:
        return

    try:
        # Read text using buffer to get raw bytes, then decode as utf-8
        input_bytes = sys.stdin.buffer.read()
        text = input_bytes.decode('utf-8')
    except Exception:
        return

    if not text or not text.strip():
        return

    try:
        pythoncom.CoInitialize()
        
        speaker = win32com.client.Dispatch("SAPI.SpVoice")
        
        # Set Voice (Huihui)
        try:
            voices = speaker.GetVoices()
            for i in range(voices.Count):
                voice = voices.Item(i)
                desc = voice.GetDescription()
                if 'Huihui' in desc or 'Chinese' in desc:
                    speaker.Voice = voice
                    break
        except:
            pass
            
        # Speed (Rate) -10 to 10
        try:
            speaker.Rate = 0 # Normal speed
        except:
            pass
            
        # 0 = Default (Sync)
        speaker.Speak(text, 0)
        
    except Exception:
        pass
    finally:
        pythoncom.CoUninitialize()

def main():
    # Check for worker flag
    if len(sys.argv) > 1 and sys.argv[1] == '--tts-worker':
        run_tts_worker()
        return

    app = QApplication(sys.argv)
    
    # Set global font
    font = app.font()
    font.setFamily("Microsoft YaHei")
    font.setPointSize(10)
    app.setFont(font)

    try:
        # Init Logic with no bank (will be loaded later)
        manager = ExamManager(None)
        
        # Init UI
        window = MainWindow(manager)
        window.show()
        
        sys.exit(app.exec())
        
    except Exception as e:
        QMessageBox.critical(None, "严重错误", f"程序启动失败:\n{str(e)}")
        print(f"Critical Error: {e}")

if __name__ == "__main__":
    main()
