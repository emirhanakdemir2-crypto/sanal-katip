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
            
            if not api_key:
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"error": "API Anahtarı Vercel'e eklenmemiş!"}).encode('utf-8'))
                return

            # Denenecek model isimleri (404 hatasını aşmak için sırayla dener)
            model_list = [
                "gemini-1.5-flash",
                "gemini-1.5-flash-latest",
                "gemini-pro"
            ]
            
            ai_cevabi = None
            son_hata = ""

            for model in model_list:
                try:
                    url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
                    payload = {
                        "contents": [{"parts": [{"text": f"Adliye katibi asistanı olarak şu metni işle: {user_text}"}]}]
                    }
                    
                    req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
                    
                    with urllib.request.urlopen(req, timeout=10) as response:
                        res_body = response.read()
                        gemini_json = json.loads(res_body.decode('utf-8'))
                        ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
                        if ai_cevabi: break # Başarılıysa döngüden çık
                except Exception as e:
                    son_hata = str(e)
                    continue # Hata verirse bir sonraki modeli dene

            if ai_cevabi:
                self.send_response(200)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))
            else:
                raise Exception(f"Tüm modeller denendi ama sonuç alınamadı. Son hata: {son_hata}")
            
        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": str(e)}).encode('utf-8'))
