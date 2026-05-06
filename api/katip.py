from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            # Arayüzden (index.html) gönderilen metni alıyoruz
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            body = json.loads(post_data.decode('utf-8'))
            user_text = body.get('text', '')

            # Güvenlik! API anahtarını kodun içine değil, Vercel'in gizli kasasına koyacağız
            api_key = os.environ.get('GEMINI_API_KEY')
            
            if not api_key:
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"error": "Sistem Hatası: API Anahtarı eksik!"}).encode('utf-8'))
                return

            # Gemini API'sine gönderilecek bağlantı
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={api_key}"
            
            # Yapay zekaya kimliğini veriyoruz
            sistem_mesaji = "Sen bir adliye katibi asistanısın. Görevin, sana verilen metinleri hukuki formata uygun şekilde düzenlemek, özetlemek veya istenen veriyi (örn: TC, Dosya No vb.) net bir şekilde ayıklamaktır."
            tam_istek = f"{sistem_mesaji}\n\nKullanıcıdan gelen metin/talep: {user_text}"
            
            payload = {
                "contents": [{"parts": [{"text": tam_istek}]}]
            }
            
            # İstek atıyoruz
            req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
            
            with urllib.request.urlopen(req) as response:
                res_body = response.read()
                gemini_json = json.loads(res_body.decode('utf-8'))
                
                # Gemini'den gelen cevabı cımbızla çekiyoruz
                ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
            
            # Başarılı cevabı sitemize (arayüze) geri gönderiyoruz
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))
            
        except Exception as e:
            # Hata olursa ekrana yazdırıyoruz
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode('utf-8'))
