import sqlite3
import os

db_path = 'Mercurius/movements.db'
if os.path.exists(db_path):
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
    tables = cursor.fetchall()
    print(f"Tables in {db_path}: {tables}")
    for table in tables:
        table_name = table[0]
        cursor.execute(f"PRAGMA table_info({table_name})")
        columns = cursor.fetchall()
        print(f"Columns in {table_name}: {columns}")
    conn.close()
else:
    print(f"Database not found at {db_path}")
