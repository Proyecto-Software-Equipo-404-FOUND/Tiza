-- Solo lectura. Ejecutar en la base/esquema objetivo antes del despliegue.
-- No se incluyen emails en la salida: los IDs permiten resolver cada caso manualmente.
BEGIN READ ONLY;
SELECT count(*) AS cantidad,
       array_agg("Id" ORDER BY "Id") AS usuarios,
       array_agg(DISTINCT "InstitucionId") AS instituciones
FROM "Usuario"
GROUP BY lower(btrim("Email", U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'))
HAVING count(*) > 1;
COMMIT;
