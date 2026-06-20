import LiteDB

db = LiteDB.LiteDatabase('Mercurius/veramay.litedb')
settings = db['AppSetting']
for doc in settings.find():
    key = doc.get('Key', '')
    if 'Shopify' in key:
        val = str(doc.get('Value', ''))[:30]
        print(f"{key}: {val}")
db.close()