// 파일을 고칠 때마다 .claude/worklog.md 에 한 줄을 남긴다.
// 같은 유니티 에디터에 여러 클로드 세션이 붙어 있어서, 누가 무슨 파일을 만졌는지
// 서로 볼 수 있어야 한다. settings.json 의 PostToolUse 훅이 이 스크립트를 부른다.
//
// 훅 입력은 stdin 으로 들어오는 JSON 한 덩어리다.
//   { session_id, tool_name, tool_input: { file_path }, tool_response: { filePath } }
//
// 이 스크립트는 절대 실패하면 안 된다. 여기서 예외가 나면 편집 자체가 막힌다.
// 그래서 전부 try 로 감싸고, 무슨 일이 있어도 0 으로 끝낸다.

const fs = require('fs');
const path = require('path');

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const logPath = path.join(root, '.claude', 'worklog.md');

let raw = '';
process.stdin.on('data', (d) => { raw += d; });
process.stdin.on('end', () => {
  try {
    write(JSON.parse(raw));
  } catch (e) {
    // 로그 한 줄 못 남기는 것보다 작업이 멈추는 쪽이 나쁘다. 조용히 넘어간다.
  }
});

function write(job) {
  let out = '';

  for (const file of targets(job)) {
    // 프로젝트 안쪽 파일만 남긴다. 바깥 파일은 다른 세션과 부딪힐 일이 없다.
    const rel = path.relative(root, file).replace(/\\/g, '/');
    if (!rel || rel.startsWith('..')) continue;

    // worklog 자기 자신은 안 남긴다. 남기면 무한히 불어난다.
    if (rel.startsWith('.claude/worklog')) continue;

    out += [stamp(), shortId(job.session_id), job.tool_name || '?', rel].join('  ') + '\n';
  }

  if (!out) return;

  // 세 세션이 동시에 쓴다. 한 줄씩 이어 붙이는 append 는 그 정도 경합은 견딘다.
  fs.appendFileSync(logPath, header() + out, 'utf8');
}

/** 이번 호출이 건드린 파일들. Edit·Write 는 스스로 말해 주고, Bash 는 아니다. */
function targets(job) {
  const direct = (job.tool_input && job.tool_input.file_path)
              || (job.tool_response && job.tool_response.filePath);
  if (direct) return [direct];

  const cmd = job.tool_input && job.tool_input.command;
  return cmd ? changedIn(cmd) : [];
}

// Bash 는 무엇을 고쳤는지 알려 주지 않는다. 명령문에서 파일처럼 생긴 토큰을 뽑아
// 그중 방금 수정된 것만 남긴다. cat·grep 처럼 읽기만 한 파일은 mtime 이 안 바뀌어 걸러진다.
const LOOKS_LIKE_FILE = /\.(cs|js|json|md|py|shader|asset|unity|prefab|txt|yml|yaml)$/i;
const FRESH_MS = 20000;

function changedIn(cmd) {
  const text = String(cmd);
  const tokens = text.match(/[^\s'"`;|&<>()]+/g) || [];

  // 따옴표 안은 공백이 있어도 한 덩어리다. "Assets/Fonts/Galmuri11 Raster.asset" 같은 것.
  let m;
  const quoted = /['"]([^'"\n]+)['"]/g;
  while ((m = quoted.exec(text)) !== null) tokens.push(m[1]);

  const found = [];
  const seen = new Set();

  for (const token of tokens) {
    if (!LOOKS_LIKE_FILE.test(token) || seen.has(token)) continue;
    seen.add(token);

    const abs = path.isAbsolute(token) ? token : path.join(root, token);
    try {
      if (Date.now() - fs.statSync(abs).mtimeMs < FRESH_MS) found.push(abs);
    } catch (e) {
      // 없는 파일이거나 경로가 아닌 토큰. 그냥 건너뛴다.
    }
  }

  return found;
}

/** 파일이 없을 때만 맨 위에 설명을 깐다. */
function header() {
  if (fs.existsSync(logPath)) return '';
  return '# 세션 작업 기록 (자동)\n\n'
       + '`.claude/worklog.js` 가 파일을 고칠 때마다 한 줄씩 남긴다. 손으로 고치지 말 것.\n'
       + '열: 시각 / 세션 앞 8자리 / 도구 / 파일\n\n';
}

function stamp() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

function shortId(id) {
  // session_id 는 local_<uuid> 꼴이다. 앞의 local_ 을 떼야 서로 구분이 된다.
  return String(id || '?').replace(/^local_/, '').slice(0, 8);
}
