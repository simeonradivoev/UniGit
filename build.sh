#! /bin/sh

project="<UniGit>"

echo "Running Tests and DLL build"
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -silent-crashes \
  -logFile $(pwd)/unity.log \
  -projectPath $(pwd) \
  -runEditorTests \
  -executeMethod UniGit.Utils.GitCiBuild.PerformBuild \
  -dlldir $(pwd)/UniGit \
  -editorTestsResultFile $(pwd)/tests.log \

echo 'Logs from build'
cat $(pwd)/unity.log

echo 'Test Results'
car $(pwd)/tests.log 

echo 'Attempting to zip builds'
zip -r $(pwd)/UniGit/build.zip $(pwd)/UniGit/