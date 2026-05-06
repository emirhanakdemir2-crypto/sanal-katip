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

            # .strip() ekleyerek şifrenin sonundaki yanlışlıkla kopyalanan boşlukları siliyoruz
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()
            
            if not api_key:
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"error": "Sistem Hatası: API Anahtarı eksik!"}).encode('utf-8'))
                return

            # Model adını 'gemini-1.5-flash-latest' olarak güncelledik (404 hatasını önlemek için)
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={api_key}"
            
            sistem_mesaji = "Sen bir adliye katibi asistanısın. Görevin, sana verilen metinleri hukuki formata uygun şekilde düzenlemek, özetlemek veya istenen veriyi (örn: TC, Dosya No vb.) net bir şekilde ayıklamaktır."
            tam_istek = f"{sistem_mesaji}\n\nKullanıcıdan gelen metin/talep: {user_text}"
            
            payload = {
                "contents": [{"parts": [{"text": tam_istek}]}]
            }
            
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            with urllib.request.urlopen(req) as response:
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
