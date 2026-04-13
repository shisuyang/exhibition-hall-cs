const fs = require('fs');
const f = 'C:/Users/User/.openclaw/workspace/exhibition-hall-cs/Views/MainForm.cs';
let c = fs.readFileSync(f, 'utf8');

// 恢复 play_video case
c = c.replace('// case "play_video":', 'case "play_video":');

// 恢复 resume case
c = c.replace('// case "resume":', 'case "resume":');

// 恢复 play_ppt/show_doc 里的 OpenPPT/ShowDoc
c = c.replace('// ShowDoc(cmd.File);', 'ShowDoc(cmd.File);');
c = c.replace('// OpenPPT(cmd.File);', 'OpenPPT(cmd.File);');

fs.writeFileSync(f, c, 'utf8');
console.log('done');
