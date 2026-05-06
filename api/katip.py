from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            # Gelen veriyi oku
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')

            # Vercel kasasındaki anahtarı al ve boşlukları temizle
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()
            
            if not api_key:
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"error": "Sistem Hatası: API Anahtarı Vercel üzerinde bulunamadı!"}).encode('utf-8'))
                return

            # Gemini 3.0 Flash API Endpoint (2026 güncel format)
            # Eğer 404 almaya devam edersen model adını 'gemini-1.5-flash' yaparak dene
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={api_key}"
            
            payload = {
                "contents": [{
                    "parts": [{
                        "text": f"Sen profesyonel bir hukuk asistanısın. Aşağıdaki metni analiz et, verileri ayıkla veya istenen işlemi yap:\n\n{user_text}"
                    }]
                }]
            }
            
            # Google sunucularına bağlan
            req = urllib.request.Request(
                url, 
                data=json.dumps(payload).encode('utf-8'), 
                headers={'Content-Type': 'application/json'}
            )
            
            with urllib.request.urlopen(req) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                
                # Cevabı al
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            # Başarılı sonucu gönder
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))
            
        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": f"API Hatası: {str(e)}"}).encode('utf-8'))
