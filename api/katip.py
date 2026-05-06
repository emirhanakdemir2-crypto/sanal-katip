from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            # En gelişmiş Gemini 3.1 Pro modeli
            model_name = "gemini-3.1-pro-preview" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            # Persona'yı tamamen temizledik, saf zeka bıraktık
            sistem_talimati = (
                "Sen en gelişmiş Gemini 3.1 sürümüsün. Kullanıcının her türlü sorusuna "
                "en derin ve doğru yanıtı vermekle görevlisin. Karmaşık sorularda önce "
                "içinden muhakeme yap, basit sorularda direkt cevap ver. Bilgi doğruluğu için "
                "Google Arama motorunu kullanmaktan çekinme. Yanıt formatın:\n"
                "[DÜŞÜNCE]\n(Muhakeme süreci)\n"
                "[CEVAP]\n(Sonuç yanıtı)"
            )
            
            payload = {
                "contents": [{"parts": [{"text": f"{sistem_talimati}\n\nKullanıcı: {user_text}"}]}],
                "tools": [{"google_search_retrieval": {}}] # Derin araştırma için arama motoru aktif
            }
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            with urllib.request.urlopen(req, timeout=60) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))
        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode('utf-8'))
