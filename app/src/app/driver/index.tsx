import { router } from 'expo-router';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { Card, EmptyNote, Status } from '@/components/ui';
import { useDriver } from '@/features/driver/driver-context';
import { driverStyles as s, statusKey } from '@/features/driver/driver-parts';
import { jobStatusMeta, jobTitle } from '@/features/jobs/job-format';
import { useLanguage } from '@/i18n';
import { useTheme } from '@/theme/theme-provider';
import { typography } from '@/theme/tokens';

export default function DriverJobs() {
  const { colors } = useTheme();
  const { t } = useLanguage();
  const { jobs, loading, offline, error, refresh } = useDriver();
  return (
    <ScrollView
      contentContainerStyle={s.screen}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={refresh} />}
    >
      <Text style={[typography.headline, { color: colors.text }]}>{t('yourJobs')}</Text>
      {offline ? <Text style={[typography.label, { color: colors.warning }]}>{t('offlineJobs')}</Text> : null}
      {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
      {!loading && jobs.length === 0 ? <EmptyNote title={t('noJobsTitle')} body={t('noJobsBody')} /> : null}
      {jobs.map((job) => (
        <Card
          key={job.id}
          onPress={() => router.push({ pathname: '/driver/[id]', params: { id: job.id } })}
          accessibilityLabel={jobTitle(job)}
        >
          <View style={s.row}>
            <Text style={[typography.bodyStrong, s.flex, { color: colors.text }]} numberOfLines={1}>
              {jobTitle(job)}
            </Text>
            <Status label={t(statusKey(job.status))} tone={jobStatusMeta(job.status).tone} />
          </View>
          <Text style={[typography.body, { color: colors.textSecondary }]} numberOfLines={1}>
            {job.destination}
          </Text>
          {job.reference ? (
            <Text style={[typography.caption, { color: colors.muted }]}>{t('order', { ref: job.reference })}</Text>
          ) : null}
        </Card>
      ))}
    </ScrollView>
  );
}
