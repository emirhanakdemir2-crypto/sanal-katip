from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request
import urllib.error

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()

            # STRATEJİ: Önce Google'ın en yeni ve en standart ismini deniyoruz
            # 2026'da yaygınlaşan 'gemini-2.0-flash' ismini deniyoruz
            model_name = "gemini-2.0-flash" 
            url = f"https://generativelanguage.googleapis.com/v1beta/models/{model_name}:generateContent?key={api_key}"
            
            payload = {"contents": [{"parts": [{"text": user_text}]}]}
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})

            try:
                with urllib.request.urlopen(req, timeout=15) as response:
                    res_body = response.read()
                    gemini_json = json.loads(res_body.decode('utf-8'))
                    ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
                
                self.send_response(200)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))

            except urllib.error.HTTPError as he:
                # EĞER YİNE 404 ALIRSAK: Google'a "Kardeşim senin elinde hangi modeller var?" diye soruyoruz
                list_url = f"https://generativelanguage.googleapis.com/v1beta/models?key={api_key}"
                with urllib.request.urlopen(list_url) as list_res:
                    models_data = json.loads(list_res.read().decode('utf-8'))
                    # Sadece isimleri ayıklıyoruz
                    available_models = [m['name'] for m in models_data.get('models', []) if 'generateContent' in m.get('supportedGenerationMethods', [])]
                
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                error_msg = f"Model bulunamadı! Senin anahtarınla kullanabileceğin modeller şunlar:\n" + "\n".join(available_models)
                self.wfile.write(json.dumps({"error": error_msg}).encode('utf-8'))

        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": f"Sistem Hatası: {str(e)}"}).encode('utf-8'))
