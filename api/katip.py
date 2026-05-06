from http.server import BaseHTTPRequestHandler
import json
import os
import urllib.request

class handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            api_key = os.environ.get('GEMINI_API_KEY', '').strip()
            
            # Google'a "Benim anahtarımla hangi modelleri kullanabilirim?" diye soruyoruz
            url = f"https://generativelanguage.googleapis.com/v1beta/models?key={api_key}"
            
            with urllib.request.urlopen(url) as response:
                data = json.loads(response.read().decode('utf-8'))
                
                # Sadece 'generateContent' destekleyen modelleri ayıklıyoruz
                modeller = []
                for m in data.get('models', []):
                    if 'generateContent' in m.get('supportedGenerationMethods', []):
                        # 'models/' kısmını atıp sadece ismi alıyoruz (örn: gemini-1.5-flash)
                        modeller.append(m['name'].replace('models/', ''))
                
                sonuc = "Kullanabileceğin Modeller:\n\n" + "\n".join(modeller)
                
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"result": sonuc}).encode('utf-8'))
            
        except Exception as e:
            self.send_response(500)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps({"error": f"Liste alınamadı: {str(e)}"}).encode('utf-8'))
