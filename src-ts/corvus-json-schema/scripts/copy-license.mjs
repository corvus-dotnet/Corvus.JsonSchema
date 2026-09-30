// Copies the repository's LICENSE into the package before it is packed, so the published package carries it.
import fs from 'node:fs';

fs.copyFileSync(new URL('../../../LICENSE', import.meta.url), new URL('../LICENSE', import.meta.url));
