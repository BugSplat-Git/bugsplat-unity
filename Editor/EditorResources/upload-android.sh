#!/bin/sh

if [ -z "$6" ]; then
  echo "Not enough arguments provided."
  exit 1
fi

echo "boi"

ZIP_FILE = $1
CLIENT_ID = $2
CLIENT_SECRET = $3
DATABASE = $4
APP_NAME = $5
APP_VERSION = $6

WORK_DIR="$PWD"

FILES_DIR = "/tmp/files"

rm -rf "${FILES_DIR}"
mkdir -p "${FILES_DIR}"

unzip "${ZIP_FILE}" -d "${FILES_DIR}"

UPLOAD_URL="https://${DATABASE}.bugsplat.com/post/android/symbols"

echo "App version: ${APP_VERSION}"
echo "Signing into bugsplat and storing session cookie for use in upload"

COOKIEPATH="/tmp/bugsplat-cookie.txt"
LOGIN_URL="https://app.bugsplat.com/oauth2/authorize"
rm "${COOKIEPATH}"
curl -b "${COOKIEPATH}" -c "${COOKIEPATH}" --data-urlencode "client_id=${CLIENT_ID}" --data-urlencode "client_secret=${CLIENT_SECRET}" --data-urlencode "grant_type=client_credentals" "${LOGIN_URL}"

cd "${FILES_DIR}"

for FILE in *; do 
echo "Uploading ${FILE} to ${UPLOAD_URL}"
curl -i -b "${COOKIEPATH}" -c "${COOKIEPATH}" -F file=@"${FILE}" -F appName="${APP_NAME}" -F appVersion="${APP_VERSION}" -F database="${DATABASE}" $UPLOAD_URL; 
done

exit 0