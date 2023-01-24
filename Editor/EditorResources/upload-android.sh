#!/bin/bash

if [ -z "$6" ]; then
  echo "Not enough arguments provided."
  exit 1
fi

export ZIP_FILE=$1
export CLIENT_ID=$2
export CLIENT_SECRET=$3
export DATABASE=$4
export APP_NAME=$5
export APP_VERSION=$6

export WORK_DIR="$PWD"

echo "${WORK_DIR}"

LOGIN_URL="https://app.bugsplat.com/oauth2/authorize"

curl -i --data-urlencode "client_id=${CLIENT_ID}" --data-urlencode "client_secret=${CLIENT_SECRET}" --data-urlencode "grant_type=client_credentals" --data-urlencode "scope=restricted" ${LOGIN_URL}

#export FILES_DIR="${WORK_DIR}/tmp/files"

#rm -rf "${FILES_DIR}"
#mkdir -p "${FILES_DIR}"

#unzip "${ZIP_FILE}" -d "${FILES_DIR}"

#UPLOAD_URL="https://${DATABASE}.bugsplat.com/post/android/symbols"

#cd "${FILES_DIR}"

#find . -name "*.so" -print0 | while read -d $'\0' FILE
#do
#echo "Uploading ${FILE} to ${UPLOAD_URL}"
#curl -i -F file=@"${FILE}" -F appName="${APP_NAME}" -F appVersion="${APP_VERSION}" -F database="${DATABASE}" $UPLOAD_URL; 
#done

exit 0