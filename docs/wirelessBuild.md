# 1) デバイスを USB ケーブルで接続
adb devices

# 2) ターゲットを TCP/IP モードで待ち受け（ポート5555）
adb tcpip 5555

# 3) USB ケーブルを抜く

# 4) Vive Focus Vision の Wi‑Fi 接続先 IP を確認
#    （ViVE focus vision の場合は，設定＞全般＞詳細＞ヘッドセットの状態）

# 5) IP で接続
adb connect <デバイスのIPアドレス>:5555

# 6) 接続確認
adb devices
# 例: 192.168.1.42:5555 device