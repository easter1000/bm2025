import os
import json
import re # 연도 추출을 위한 정규식 라이브러리

# --- 설정 ---
INPUT_JSON_FILE = 'players.json'
SALARY_TEXT_FILE = 'salary_data.txt' # 사용자가 제공한 텍스트 파일
OUTPUT_JSON_FILE = 'players_with_salary.json'

def normalize_name(name):
    """이름을 비교하기 쉽도록 정규화합니다."""
    return ''.join(c for c in name if c.isalnum()).lower()

def clean_salary(salary_str):
    """'$50,123,456' 같은 문자열을 정수 50123456으로 변환합니다."""
    try:
        return int(salary_str.strip().replace('$', '').replace(',', ''))
    except (ValueError, AttributeError):
        return 0

def parse_and_process_salaries(file_path):
    """
    salary_data.txt 파일을 읽고, 사용자 규칙에 따라 계약 정보를 가공합니다.
    """
    print(f"'{file_path}' 파일에서 연봉 데이터 파싱 및 가공을 시작합니다...")
    
    player_contracts = {}
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except FileNotFoundError:
        print(f"오류: '{file_path}' 파일을 찾을 수 없습니다. 스크립트와 같은 폴더에 있는지 확인해주세요.")
        return None

    for line in lines:
        cols = line.strip().split('\t')
        
        if len(cols) < 4:
            continue

        if not cols[0].strip().isdigit():
            continue

        player_name = cols[1].strip()
        salary_2025_26 = clean_salary(cols[3])
        
        actual_years_left = 0
        for i in range(3, len(cols)):
            if '$' not in cols[i]:
                break
            if clean_salary(cols[i]) > 0:
                actual_years_left += 1

        if salary_2025_26 > 0 and actual_years_left > 0:
            simulated_years_left = actual_years_left + 1
            simulated_contract_value = salary_2025_26 * simulated_years_left
        else:
            simulated_years_left = 0
            simulated_contract_value = 0

        normalized = normalize_name(player_name)
        player_contracts[normalized] = {
            'name': player_name,
            'contract_years_left': simulated_years_left,
            'contract_value': simulated_contract_value
        }
            
    print(f"총 {len(player_contracts)}명의 계약 데이터를 텍스트 파일로부터 성공적으로 가공했습니다.")
    return player_contracts

def merge_data(original_players, contract_data):
    """원본 선수 데이터에 가공된 계약 데이터를 병합합니다."""
    print("\n원본 데이터와 계약 데이터를 병합합니다...")
    
    updated_count = 0
    not_found_players = []

    for player in original_players:
        player_name = player.get('name')
        if not player_name: continue
            
        normalized_player_name = normalize_name(player_name)
        
        if normalized_player_name in contract_data:
            info = contract_data[normalized_player_name]
            player['contract_years_left'] = info['contract_years_left']
            player['contract_value'] = info['contract_value']
            updated_count += 1
        else:
            # [수정됨] 계약 정보를 찾지 못한 경우, contract_years_left를 -1로 설정합니다.
            player['contract_years_left'] = -1
            player['contract_value'] = 0
            not_found_players.append(player_name)
            
    print(f"총 {updated_count}명의 선수 정보가 업데이트되었습니다.")
    if not_found_players:
        print(f"\n경고: 다음 {len(not_found_players)}명의 선수 정보를 찾지 못했습니다 (FA, 신인 등):")
        print(", ".join(not_found_players[:20]) + ("..." if len(not_found_players) > 20 else ""))
        
    return original_players

def main():
    """메인 실행 함수"""
    contract_data = parse_and_process_salaries(SALARY_TEXT_FILE)
    if not contract_data:
        print("\n계약 데이터를 가져오지 못해 프로그램을 종료합니다.")
        return

    try:
        with open(INPUT_JSON_FILE, 'r', encoding='utf-8') as f:
            original_data = json.load(f)
            original_players = original_data.get('players', [])
    except FileNotFoundError:
        print(f"오류: '{INPUT_JSON_FILE}' 파일을 찾을 수 없습니다.")
        return
        
    updated_players = merge_data(original_players, contract_data)
    
    output_data = {'players': updated_players}
    with open(OUTPUT_JSON_FILE, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=2)
        
    print(f"\n모든 작업이 완료되었습니다! 결과가 '{OUTPUT_JSON_FILE}' 파일에 저장되었습니다.")

if __name__ == '__main__':
    main()