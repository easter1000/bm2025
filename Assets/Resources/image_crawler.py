import os
import json
import requests
import time

# --- 설정 ---
INPUT_JSON_FILE = 'players.json'
OUTPUT_FOLDER = 'player_photos'
IMAGE_URL_TEMPLATE = 'https://cdn.nba.com/headshots/nba/latest/1040x760/{player_id}.png'

HEADERS = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36',
    'Referer': 'https://www.nba.com/'
}

def load_players_from_json(file_path):
    """players.json 파일에서 선수 목록을 로드합니다."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        print(f"'{file_path}'에서 {len(data.get('players', []))}명의 선수 데이터를 로드했습니다.")
        return data.get('players', [])
    except FileNotFoundError:
        print(f"오류: '{file_path}' 파일을 찾을 수 없습니다. 스크립트와 같은 폴더에 있는지 확인하세요.")
        return None
    except json.JSONDecodeError:
        print(f"오류: '{file_path}' 파일이 올바른 JSON 형식이 아닙니다.")
        return None

# [수정됨] sanitize_filename 함수는 더 이상 필요 없으므로 삭제했습니다.

def download_player_photo(player, output_dir):
    """선수 ID를 기반으로 이미지 URL을 생성하고 다운로드합니다."""
    player_id = player.get('player_id')
    player_name = player.get('name') # 로그 출력을 위해 이름은 계속 사용합니다.

    if not player_id or not player_name:
        print("경고: player_id 또는 name이 없는 데이터가 있어 건너뜁니다.")
        return

    print(f"처리 중: {player_name} (ID: {player_id})...")

    try:
        image_url = IMAGE_URL_TEMPLATE.format(player_id=player_id)
        
        image_response = requests.get(image_url, headers=HEADERS, timeout=10)
        image_response.raise_for_status()

        # [수정됨] 파일 이름을 player_id를 사용하여 생성합니다.
        filename = f"{player_id}.png"
        output_path = os.path.join(output_dir, filename)

        with open(output_path, 'wb') as f:
            f.write(image_response.content)
        
        print(f"-> 성공: '{filename}'으로 저장했습니다.")

    except requests.exceptions.HTTPError as e:
        print(f"-> 실패: {player_name}의 사진을 찾을 수 없습니다 (HTTP Error: {e.response.status_code}). 건너뜁니다.")
    except requests.exceptions.RequestException as e:
        print(f"-> 실패: {player_name} 처리 중 네트워크 오류 발생: {e}")
    except Exception as e:
        print(f"-> 실패: {player_name} 처리 중 알 수 없는 오류 발생: {e}")

def main():
    """메인 실행 함수"""
    print("NBA 선수 사진 크롤러 (ID 기반 저장)를 시작합니다.")
    
    players = load_players_from_json(INPUT_JSON_FILE)
    if not players:
        return

    if not os.path.exists(OUTPUT_FOLDER):
        os.makedirs(OUTPUT_FOLDER)
        print(f"'{OUTPUT_FOLDER}' 폴더를 생성했습니다.")

    total_players = len(players)
    for i, player in enumerate(players):
        download_player_photo(player, OUTPUT_FOLDER)
        time.sleep(0.2)
        print(f"--- 진행률: {i+1}/{total_players} ---")

    print("\n모든 작업이 완료되었습니다!")
    print(f"사진은 '{OUTPUT_FOLDER}' 폴더에 저장되었습니다.")

if __name__ == '__main__':
    main()