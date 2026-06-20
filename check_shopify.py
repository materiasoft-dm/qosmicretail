import sqlite3
conn = sqlite3.connect('Mercurius/veramay.db')
cursor = conn.cursor()
cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = cursor.fetchall()
print("Tables:", [t[0] for t in tables])

cursor.execute("SELECT Key, Value FROM AppSetting WHERE Key LIKE '%Shopify%'")
rows = cursor.fetchall()
for r in rows:
    val = r[1][:30] + "..." if len(r[1]) > 30 else r[1]
    print(f"{r[0]}: {val}")
if not rows:
    print("No Shopify settings found")
conn.close()