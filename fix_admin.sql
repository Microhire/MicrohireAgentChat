-- Grant SysAdmin to Jenny Junkeer (ID 260) in local AITESTDB
-- Run on the VM with: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\fix_admin.sql

UPDATE tblOperators SET SysAdmin = 1 WHERE ID = 260;

-- Verify
SELECT ID, FirstName, LastName, Loginname, SysAdmin, LoginAllowed, BelongsToGroup
FROM tblOperators WHERE ID = 260;

PRINT 'Done — Jenny Junkeer is now SysAdmin. Retry DatabaseWizard.exe with login: JENNY JUNKEER / jj';
