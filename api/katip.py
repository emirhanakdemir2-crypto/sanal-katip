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
            
            # Google'ın istediği gibi v1beta ve gemini-1.5-flash-latest kombinasyonuna geçiyoruz
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={api_key}"
            
            payload = {
                "contents": [{
                    "parts": [{"text": f"Bir adliye katibi asistanı olarak şu metni profesyonelce düzenle veya istenen işlemi yap:\n\n{user_text}"}]
                }]
            }
            
            req = urllib.request.Request(
                url, 
                data=json.dumps(payload).encode('utf-8'), 
                headers={'Content-Type': 'application/json'}
            )
            
            try:
                with urllib.request.urlopen(req, timeout=20) as response:
                    res_body = response.read()
                    gemini_json = json.loads(res_body.decode('utf-8'))
                    ai_cevabi = gemini_json['candidates'][0]['content']['parts'][0]['text']
                
                self.send_response(200)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"result": ai_cevabi}).encode('utf-8'))
                
            except urllib.error.HTTPError as he:
                error_details = he.read().decode('utf-8')
                self.send_response(500)
                self.send_header('Content-type', 'application/json')
                self.end_headers()
                self.wfile.write(json.dumps({"error": f"Google API Hatası: {error_details}"}).encode('utf-8'))
                
        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": f"Sistem Hatası: {str(e)}"}).encode('utf-8'))
