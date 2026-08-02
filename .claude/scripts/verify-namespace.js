#!/usr/bin/env node
/**
 * Verify C# file namespace matches file path
 *
 * Usage: node verify-namespace.js <file-path>
 * Example: node verify-namespace.js src/Domain/Entities/Game.cs
 *
 * Exit codes:
 *   0 - All namespaces match
 *   1 - Namespace mismatch found
 *   2 - File not found or invalid input
 */

const fs = require('fs');
const path = require('path');

// Map of directory to expected namespace segment
const directoryToNamespaceMap = {
  'Domain': 'Domain',
  'Application': 'Application',
  'Infrastructure': 'Infrastructure',
  'Api': 'Api',
  'Tests': 'Tests',
};

// Standard namespace prefix for this project
const PROJECT_NAMESPACE_PREFIX = 'BoardGameAiDashboard';

function getExpectedNamespace(filePath) {
  const parts = filePath.split(path.sep);
  const segments = [];

  for (const part of parts) {
    if (directoryToNamespaceMap[part]) {
      segments.push(directoryToNamespaceMap[part]);
    }
  }

  if (segments.length === 0) {
    return null;
  }

  return `${PROJECT_NAMESPACE_PREFIX}.${segments.join('.')}`;
}

function extractNamespace(fileContent) {
  const match = fileContent.match(/namespace\s+([\w.]+)/);
  return match ? match[1] : null;
}

function verifyFile(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(`Error: File not found: ${filePath}`);
    process.exit(2);
  }

  const content = fs.readFileSync(filePath, 'utf-8');
  const actualNamespace = extractNamespace(content);

  if (!actualNamespace) {
    console.log(`⚠️  ${filePath}: No namespace found (may be a script file)`);
    return { valid: true, file: filePath };
  }

  const expectedNamespace = getExpectedNamespace(filePath);

  if (!expectedNamespace) {
    console.log(`⚠️  ${filePath}: Could not determine expected namespace`);
    return { valid: true, file: filePath };
  }

  if (actualNamespace !== expectedNamespace) {
    console.error(`❌ Namespace mismatch: ${filePath}`);
    console.error(`   Found:     ${actualNamespace}`);
    console.error(`   Expected:  ${expectedNamespace}`);
    return { valid: false, file: filePath, found: actualNamespace, expected: expectedNamespace };
  }

  console.log(`✅ ${filePath}: ${actualNamespace}`);
  return { valid: true, file: filePath };
}

function verifyDirectory(dirPath, pattern = '**/*.cs') {
  const glob = require('glob');
  const files = glob.sync(pattern, { cwd: dirPath, absolute: true });

  const results = [];
  for (const file of files) {
    results.push(verifyFile(file));
  }

  return results;
}

// Main execution
const args = process.argv.slice(2);

if (args.length === 0) {
  console.log('Usage: node verify-namespace.js <file-or-directory>');
  console.log('');
  console.log('Examples:');
  console.log('  node verify-namespace.js src/Domain/Entities/Game.cs');
  console.log('  node verify-namespace.js src/Domain');
  console.log('  node verify-namespace.js .');
  process.exit(0);
}

const target = args[0];
const stats = fs.statSync(target);

let results = [];

if (stats.isDirectory()) {
  console.log(`Verifying all C# files in: ${target}\n`);
  results = verifyDirectory(target);
} else {
  console.log(`Verifying: ${target}\n`);
  results = [verifyFile(target)];
}

// Summary
const invalidResults = results.filter(r => !r.valid);
const total = results.length;
const passed = total - invalidResults.length;

console.log('\n--- Summary ---');
console.log(`Total files: ${total}`);
console.log(`Passed: ${passed}`);
console.log(`Failed: ${invalidResults.length}`);

if (invalidResults.length > 0) {
  console.log('\n--- Failed Files ---');
  for (const r of invalidResults) {
    console.log(`  ${r.file}`);
    console.log(`    Found:     ${r.found}`);
    console.log(`    Expected:  ${r.expected}`);
  }
  process.exit(1);
}

process.exit(0);
